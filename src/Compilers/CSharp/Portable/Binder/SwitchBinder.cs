﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal partial class SwitchBinder : LocalScopeBinder
    {
        protected readonly SwitchStatementSyntax _switchSyntax;
        protected readonly GeneratedLabelSymbol _breakLabel;

        private BoundExpression _switchGoverningExpression;
        private DiagnosticBag _switchGoverningDiagnostics;

        protected SwitchBinder(Binder next, SwitchStatementSyntax switchSyntax)
            : base(next)
        {
            _switchSyntax = switchSyntax;
            _breakLabel = new GeneratedLabelSymbol("break");
        }

        internal static SwitchBinder Create(Binder next, SwitchStatementSyntax switchSyntax)
        {
            var parseOptions = switchSyntax?.SyntaxTree?.Options as CSharpParseOptions;
            return
                // in C# 6 and earlier, we use the old binder. In C# 7 and later, we use the new binder which
                // is capable of binding both the old and new syntax. However, the new binder does not yet
                // lead to a translation that fully supports edit-and-continue.
                (parseOptions?.IsFeatureEnabled(MessageID.IDS_FeaturePatternMatching) != false || parseOptions?.Features.ContainsKey("typeswitch") != false)
                ? new PatternSwitchBinder(next, switchSyntax)
                : new SwitchBinder(next, switchSyntax);
        }


        protected bool PatternsEnabled =>
            ((CSharpParseOptions)this._switchSyntax.SyntaxTree.Options)?.IsFeatureEnabled(MessageID.IDS_FeaturePatternMatching) != false;

        protected BoundExpression SwitchGoverningExpression
        {
            get
            {
                if (_switchGoverningExpression == null)
                {
                    var switchGoverningDiagnostics = new DiagnosticBag();
                    var boundSwitchExpression = BindSwitchExpression(_switchSyntax.Expression, switchGoverningDiagnostics);
                    _switchGoverningDiagnostics = switchGoverningDiagnostics;
                    Interlocked.CompareExchange(ref _switchGoverningExpression, boundSwitchExpression, null);
                }

                return _switchGoverningExpression;
            }
        }

        protected TypeSymbol SwitchGoverningType => SwitchGoverningExpression.Type;

        protected DiagnosticBag SwitchGoverningDiagnostics
        {
            get
            {
                var discarded = SwitchGoverningExpression;
                return _switchGoverningDiagnostics;
            }
        }

        // Dictionary for the switch case/default labels.
        // Case labels with a non-null constant value are indexed on their ConstantValue.
        // Default label(s) are indexed on a special DefaultKey object.
        // Invalid case labels with null constant value are indexed on the labelName.
        private Dictionary<object, List<SourceLabelSymbol>> _lazySwitchLabelsMap;
        private static readonly object s_defaultKey = new object();

        private Dictionary<object, List<SourceLabelSymbol>> SwitchLabelsMap
        {
            get
            {
                if (_lazySwitchLabelsMap == null && this.Labels.Length > 0)
                {
                    _lazySwitchLabelsMap = BuildMap(this.Labels);
                }

                return _lazySwitchLabelsMap;
            }
        }

        private static Dictionary<object, List<SourceLabelSymbol>> BuildMap(ImmutableArray<LabelSymbol> labels)
        {
            Debug.Assert(labels.Length > 0);

            var map = new Dictionary<object, List<SourceLabelSymbol>>(labels.Length, new SwitchConstantValueHelper.SwitchLabelsComparer());
            foreach (SourceLabelSymbol label in labels)
            {
                SyntaxKind labelKind = label.IdentifierNodeOrToken.Kind();

                if (labelKind == SyntaxKind.CaseSwitchLabel ||
                    labelKind == SyntaxKind.DefaultSwitchLabel)
                {
                    object key;
                    var constantValue = label.SwitchCaseLabelConstant;
                    if (constantValue != null)
                    {
                        // Case labels with a non-null constant value are indexed on their ConstantValue.
                        key = constantValue;
                    }
                    else if (labelKind == SyntaxKind.DefaultSwitchLabel)
                    {
                        // Default label(s) are indexed on a special DefaultKey object.
                        key = s_defaultKey;
                    }
                    else
                    {
                        // Invalid case labels with null constant value are indexed on the labelName.
                        key = label.Name;
                    }

                    List<SourceLabelSymbol> labelsList;
                    if (!map.TryGetValue(key, out labelsList))
                    {
                        labelsList = new List<SourceLabelSymbol>();
                        map.Add(key, labelsList);
                    }

                    Debug.Assert(!labelsList.Contains(label));
                    labelsList.Add(label);
                }
            }

            return map;
        }

        override protected ImmutableArray<LocalSymbol> BuildLocals()
        {
            var builder = ArrayBuilder<LocalSymbol>.GetInstance();

            foreach (var section in _switchSyntax.Sections)
            {
                builder.AddRange(BuildLocals(section.Statements));
            }

            return builder.ToImmutableAndFree();
        }

        protected override ImmutableArray<LocalFunctionSymbol> BuildLocalFunctions()
        {
            var builder = ArrayBuilder<LocalFunctionSymbol>.GetInstance();

            foreach (var section in _switchSyntax.Sections)
            {
                builder.AddRange(BuildLocalFunctions(section.Statements));
            }

            return builder.ToImmutableAndFree();
        }

        internal override bool IsLocalFunctionsScopeBinder
        {
            get
            {
                return true;
            }
        }

        internal override GeneratedLabelSymbol BreakLabel
        {
            get
            {
                return _breakLabel;
            }
        }

        protected override ImmutableArray<LabelSymbol> BuildLabels()
        {
            ArrayBuilder<LabelSymbol> labels = null;

            foreach (var section in _switchSyntax.Sections)
            {
                // add switch case/default labels
                BuildSwitchLabels(section.Labels, GetBinder(section), ref labels);

                // add regular labels from the statements in the switch section
                base.BuildLabels(section.Statements, ref labels);
            }

            return (labels != null) ? labels.ToImmutableAndFree() : ImmutableArray<LabelSymbol>.Empty;
        }

        internal override bool IsLabelsScopeBinder
        {
            get
            {
                return true;
            }
        }

        private void BuildSwitchLabels(SyntaxList<SwitchLabelSyntax> labelsSyntax, Binder sectionBinder, ref ArrayBuilder<LabelSymbol> labels)
        {
            TypeSymbol switchGoverningType = null;

            // add switch case/default labels
            foreach (var labelSyntax in labelsSyntax)
            {
                ConstantValue boundLabelConstantOpt = null;
                switch (labelSyntax.Kind())
                {
                    case SyntaxKind.CaseSwitchLabel:
                        // Bind the switch expression and the switch case label expression, but do not report any diagnostics here.
                        // Diagnostics will be reported during binding.
                        var caseLabel = (CaseSwitchLabelSyntax)labelSyntax;
                        Debug.Assert(caseLabel.Value != null);
                        DiagnosticBag tempDiagnosticBag = DiagnosticBag.GetInstance();

                        var boundLabelExpression = sectionBinder.BindValue(caseLabel.Value, tempDiagnosticBag, BindValueKind.RValue);

                        if ((object)switchGoverningType == null)
                        {
                            switchGoverningType = this.SwitchGoverningType;
                        }

                        boundLabelExpression = ConvertCaseExpression(switchGoverningType, labelSyntax, boundLabelExpression, sectionBinder, ref boundLabelConstantOpt, tempDiagnosticBag);

                        tempDiagnosticBag.Free();
                        break;

                    case SyntaxKind.DefaultSwitchLabel:
                        break;

                    case SyntaxKind.CasePatternSwitchLabel:
                        continue;

                    default:
                        throw ExceptionUtilities.UnexpectedValue(labelSyntax.Kind());
                }

                if (labels == null)
                {
                    labels = ArrayBuilder<LabelSymbol>.GetInstance();
                }

                // Create the label symbol
                labels.Add(new SourceLabelSymbol((MethodSymbol)this.ContainingMemberOrLambda, labelSyntax, boundLabelConstantOpt));
            }
        }

        protected BoundExpression ConvertCaseExpression(TypeSymbol switchGoverningType, CSharpSyntaxNode node, BoundExpression caseExpression, Binder sectionBinder, ref ConstantValue constantValueOpt, DiagnosticBag diagnostics, bool isGotoCaseExpr = false)
        {
            if (isGotoCaseExpr)
            {
                // SPEC VIOLATION for Dev10 COMPATIBILITY:

                // Dev10 compiler violates the SPEC comment below:
                //      "if the constant-expression is not implicitly convertible (§6.1) to 
                //      the governing type of the nearest enclosing switch statement, 
                //      a compile-time error occurs"

                // If there is no implicit conversion from gotoCaseExpression to switchGoverningType,
                // but there exists an explicit conversion, Dev10 compiler generates a warning "WRN_GotoCaseShouldConvert"
                // instead of an error. See test "CS0469_NoImplicitConversionWarning".

                HashSet<DiagnosticInfo> useSiteDiagnostics = null;
                Conversion conversion = Conversions.ClassifyConversionFromExpression(caseExpression, switchGoverningType, ref useSiteDiagnostics);
                diagnostics.Add(node, useSiteDiagnostics);
                if (!conversion.IsValid)
                {
                    GenerateImplicitConversionError(diagnostics, node, conversion, caseExpression, switchGoverningType);
                }
                else if (!conversion.IsImplicit)
                {
                    diagnostics.Add(ErrorCode.WRN_GotoCaseShouldConvert, node.Location, switchGoverningType);
                }

                caseExpression = CreateConversion(caseExpression, conversion, switchGoverningType, diagnostics);
            }

            return ConvertPatternExpression(switchGoverningType, node, caseExpression, ref constantValueOpt, diagnostics);
        }

        private List<SourceLabelSymbol> FindMatchingSwitchCaseLabels(ConstantValue constantValue, SyntaxNodeOrToken labelSyntax = default(SyntaxNodeOrToken))
        {
            // SwitchLabelsMap: Dictionary for the switch case/default labels.
            // Case labels with a non-null constant value are indexed on their ConstantValue.
            // Invalid case labels with null constant value are indexed on the labelName.

            object key;
            if (constantValue != null)
            {
                key = constantValue;
            }
            else
            {
                key = labelSyntax.ToString();
            }

            return FindMatchingSwitchLabels(key);
        }

        private List<SourceLabelSymbol> GetDefaultLabels()
        {
            // SwitchLabelsMap: Dictionary for the switch case/default labels.
            // Default label(s) are indexed on a special DefaultKey object.

            return FindMatchingSwitchLabels(s_defaultKey);
        }

        private static readonly List<SourceLabelSymbol> s_emptyLabelsList = new List<SourceLabelSymbol>();
        private List<SourceLabelSymbol> FindMatchingSwitchLabels(object key)
        {
            Debug.Assert(key != null);

            var labelsMap = this.SwitchLabelsMap;
            if (labelsMap != null)
            {
                List<SourceLabelSymbol> labels;
                if (labelsMap.TryGetValue(key, out labels))
                {
                    Debug.Assert(labels != null && !labels.IsEmpty());
                    return labels;
                }
            }

            return s_emptyLabelsList;
        }

        internal override ImmutableArray<LocalSymbol> GetDeclaredLocalsForScope(CSharpSyntaxNode scopeDesignator)
        {
            if (_switchSyntax == scopeDesignator)
            {
                return this.Locals;
            }

            throw ExceptionUtilities.Unreachable;
        }

        internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(CSharpSyntaxNode scopeDesignator)
        {
            if (_switchSyntax == scopeDesignator)
            {
                return this.LocalFunctions;
            }

            throw ExceptionUtilities.Unreachable;
        }
        
        internal override SyntaxNode ScopeDesignator
        {
            get
            {
                return _switchSyntax;
            }
        }

        # region "Switch statement binding methods"

        internal override BoundStatement BindSwitchExpressionAndSections(SwitchStatementSyntax node, Binder originalBinder, DiagnosticBag diagnostics)
        {
            Debug.Assert(_switchSyntax.Equals(node));

            // Bind switch expression and set the switch governing type.
            var boundSwitchExpression = this.SwitchGoverningExpression;
            diagnostics.AddRange(this.SwitchGoverningDiagnostics);

            // Switch expression might be a constant expression.
            // For this scenario we can determine the target label of the switch statement
            // at compile time.            
            LabelSymbol constantTargetOpt = null;
            var constantValue = boundSwitchExpression.ConstantValue;
            if (constantValue != null)
            {
                constantTargetOpt = BindConstantJumpTarget(constantValue);
            }
            else if (!node.Sections.Any())
            {
                // empty switch block, set the break label as target
                constantTargetOpt = this.BreakLabel;
            }

            // Bind switch section
            ImmutableArray<BoundSwitchSection> boundSwitchSections = BindSwitchSections(node.Sections, originalBinder, diagnostics);

            return new BoundSwitchStatement(node, null, boundSwitchExpression, constantTargetOpt, 
                                            GetDeclaredLocalsForScope(node), 
                                            GetDeclaredLocalFunctionsForScope(node), boundSwitchSections, this.BreakLabel, null);
        }

        // Bind the switch expression
        private BoundExpression BindSwitchExpression(ExpressionSyntax node, DiagnosticBag diagnostics)
        {
            // We are at present inside the switch binder, but the switch expression is not
            // bound in the context of the switch binder; it's bound in the context of the
            // enclosing binder. For example: 
            // 
            // class C { 
            //   int x; 
            //   void M() {
            //     switch(x) {
            //       case 1:
            //         int x;
            //
            // The "x" in "switch(x)" refers to this.x, not the local x that is in scope inside the switch block.

            Debug.Assert(node == _switchSyntax.Expression);
            var binder = this.GetBinder(node);
            Debug.Assert(binder != null);

            var switchExpression = binder.BindValue(node, diagnostics, BindValueKind.RValue);

            var switchGoverningType = switchExpression.Type;

            if ((object)switchGoverningType != null && !switchGoverningType.IsErrorType())
            {
                // SPEC:    The governing type of a switch statement is established by the switch expression.
                // SPEC:    1) If the type of the switch expression is sbyte, byte, short, ushort, int, uint,
                // SPEC:       long, ulong, bool, char, string, or an enum-type, or if it is the nullable type
                // SPEC:       corresponding to one of these types, then that is the governing type of the switch statement. 
                // SPEC:    2) Otherwise if exactly one user-defined implicit conversion (§6.4) exists from the
                // SPEC:       type of the switch expression to one of the following possible governing types:
                // SPEC:       sbyte, byte, short, ushort, int, uint, long, ulong, char, string, or, a nullable type
                // SPEC:       corresponding to one of those types, then the result is the switch governing type
                // SPEC:    3) Otherwise (in C# 7 and later) the switch governing type is the type of the
                // SPEC:       switch expression.

                if (switchGoverningType.IsValidV6SwitchGoverningType())
                {
                    // Condition (1) satisfied

                    // Note: dev11 actually checks the stripped type, but nullable was introduced at the same
                    // time, so it doesn't really matter.
                    if (switchGoverningType.SpecialType == SpecialType.System_Boolean)
                    {
                        // GetLocation() so that it also works in speculative contexts.
                        CheckFeatureAvailability(node.GetLocation(), MessageID.IDS_FeatureSwitchOnBool, diagnostics);
                    }

                    return switchExpression;
                }
                else
                {
                    TypeSymbol resultantGoverningType;
                    HashSet<DiagnosticInfo> useSiteDiagnostics = null;
                    Conversion conversion = binder.Conversions.ClassifyImplicitUserDefinedConversionForV6SwitchGoverningType(switchGoverningType, out resultantGoverningType, ref useSiteDiagnostics);
                    diagnostics.Add(node, useSiteDiagnostics);
                    if (conversion.IsValid)
                    {
                        // Condition (2) satisfied
                        Debug.Assert(conversion.Kind == ConversionKind.ImplicitUserDefined);
                        Debug.Assert(conversion.Method.IsUserDefinedConversion());
                        Debug.Assert(conversion.UserDefinedToConversion.IsIdentity);
                        Debug.Assert(resultantGoverningType.IsValidV6SwitchGoverningType(isTargetTypeOfUserDefinedOp: true));
                        return binder.CreateConversion(node, switchExpression, conversion, false, resultantGoverningType, diagnostics);
                    }
                    else if (switchGoverningType.SpecialType != SpecialType.System_Void && PatternsEnabled)
                    {
                        // Otherwsie (3) satisfied
                        return switchExpression;
                    }
                    else
                    {
                        // We need to create an error type here as certain diagnostics generated during binding the switch case label expression and
                        // goto case expression should be generated only if the switch expression type is a valid switch governing type.
                        switchGoverningType = CreateErrorType(switchGoverningType.Name);
                    }
                }
            }

            if (!switchExpression.HasAnyErrors)
            {
                if (PatternsEnabled)
                {
                    diagnostics.Add(ErrorCode.ERR_PatternValueExpected, node.Location, switchExpression.Display);
                }
                else
                {
                    diagnostics.Add(ErrorCode.ERR_V6SwitchGoverningTypeValueExpected, node.Location);
                }
            }

            return new BoundBadExpression(node, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray.Create<BoundNode>(switchExpression), switchGoverningType ?? CreateErrorType());
        }

        private LabelSymbol BindConstantJumpTarget(ConstantValue constantValue)
        {
            LabelSymbol boundLabel = null;

            // If the switch statement has a matching switch case label with the same constant
            // value, that label is set as the target.
            List<SourceLabelSymbol> labelSymbols = FindMatchingSwitchCaseLabels(constantValue);

            if (!labelSymbols.IsEmpty())
            {
                boundLabel = labelSymbols[0];
            }
            else
            {
                // If not, we check if there is a default label in the
                // switch statement and set that as the target label.
                labelSymbols = GetDefaultLabels();

                if (!labelSymbols.IsEmpty())
                {
                    boundLabel = labelSymbols[0];
                }
                else
                {
                    // Otherwise the switch statement's BreakLabel is set as the target.
                    boundLabel = BreakLabel;
                }
            }

            return boundLabel;
        }

        private ImmutableArray<BoundSwitchSection> BindSwitchSections(SyntaxList<SwitchSectionSyntax> switchSections, Binder originalBinder, DiagnosticBag diagnostics)
        {
            // Bind switch sections
            var boundSwitchSectionsBuilder = ArrayBuilder<BoundSwitchSection>.GetInstance();
            foreach (var sectionSyntax in switchSections)
            {
                boundSwitchSectionsBuilder.Add(BindSwitchSection(sectionSyntax, originalBinder, diagnostics));
            }

            return boundSwitchSectionsBuilder.ToImmutableAndFree();
        }

        private BoundSwitchSection BindSwitchSection(SwitchSectionSyntax node, Binder originalBinder, DiagnosticBag diagnostics)
        {
            var sectionBinder = originalBinder.GetBinder(node);

            // Bind switch section labels
            var boundLabelsBuilder = ArrayBuilder<BoundSwitchLabel>.GetInstance();
            foreach (var labelSyntax in node.Labels)
            {
                BoundSwitchLabel boundLabel = BindSwitchSectionLabel(labelSyntax, sectionBinder, diagnostics);
                boundLabelsBuilder.Add(boundLabel);
            }

            // Bind switch section statements
            var boundStatementsBuilder = ArrayBuilder<BoundStatement>.GetInstance();
            foreach (var statement in node.Statements)
            {
                boundStatementsBuilder.Add(sectionBinder.BindStatement(statement, diagnostics));
            }

            return new BoundSwitchSection(node, boundLabelsBuilder.ToImmutableAndFree(), boundStatementsBuilder.ToImmutableAndFree());
        }

        private BoundSwitchLabel BindSwitchSectionLabel(SwitchLabelSyntax node, Binder sectionBinder, DiagnosticBag diagnostics)
        {
            var switchGoverningType = SwitchGoverningType;
            BoundExpression boundLabelExpressionOpt = null;

            SourceLabelSymbol boundLabelSymbol = null;
            ConstantValue labelExpressionConstant = null;
            List<SourceLabelSymbol> matchedLabelSymbols;

            // Prevent cascading diagnostics
            bool hasErrors = node.HasErrors;

            switch (node.Kind())
            {
                case SyntaxKind.CaseSwitchLabel:
                    var caseLabelSyntax = (CaseSwitchLabelSyntax)node;
                    // Bind the label case expression
                    boundLabelExpressionOpt = sectionBinder.BindValue(caseLabelSyntax.Value, diagnostics, BindValueKind.RValue);
                    boundLabelExpressionOpt = ConvertCaseExpression(switchGoverningType, caseLabelSyntax, boundLabelExpressionOpt, sectionBinder, ref labelExpressionConstant, diagnostics);

                    // Check for bind errors
                    hasErrors = hasErrors || boundLabelExpressionOpt.HasAnyErrors;

                    // SPEC:    The constant expression of each case label must denote a value that
                    // SPEC:    is implicitly convertible (§6.1) to the governing type of the switch statement.
                    if (!hasErrors && labelExpressionConstant == null)
                    {
                        diagnostics.Add(ErrorCode.ERR_ConstantExpected, caseLabelSyntax.Value.Location);
                        hasErrors = true;
                    }

                    // LabelSymbols for all the switch case labels are created by BuildLabels().
                    // Fetch the matching switch case label symbols
                    matchedLabelSymbols = FindMatchingSwitchCaseLabels(labelExpressionConstant, caseLabelSyntax);
                    break;
                case SyntaxKind.CasePatternSwitchLabel:
                    if (!node.HasErrors)
                    {
                        // This should not occur, because it would have been a syntax error
                        throw ExceptionUtilities.UnexpectedValue(node.Kind());
                    }
                    matchedLabelSymbols = new List<SourceLabelSymbol>();
                    break;
                case SyntaxKind.DefaultSwitchLabel:
                    matchedLabelSymbols = GetDefaultLabels();
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(node.Kind());
            }

            // Get the corresponding matching label symbol created during BuildLabels()
            // and also check for duplicate case labels.

            Debug.Assert(hasErrors || !matchedLabelSymbols.IsEmpty());
            bool first = true;
            bool hasDuplicateErrors = false;
            foreach (SourceLabelSymbol label in matchedLabelSymbols)
            {
                if (node.Equals(label.IdentifierNodeOrToken.AsNode()))
                {
                    // we must have exactly one matching label created during BuildLabels()
                    boundLabelSymbol = label;

                    // SPEC:    A compile-time error occurs if two or more case labels
                    // SPEC:    in the same switch statement specify the same constant value.

                    if (!hasErrors && !first)
                    {
                        // Skipping the first label symbol ensures that the errors (if any),
                        // are reported on all but the first duplicate case label.
                        diagnostics.Add(ErrorCode.ERR_DuplicateCaseLabel, node.Location,
                            label.SwitchCaseLabelConstant?.Value ?? label.Name);
                        hasDuplicateErrors = true;
                    }
                    break;
                }
                first = false;
            }

            if ((object)boundLabelSymbol == null)
            {
                Debug.Assert(hasErrors);
                boundLabelSymbol = new SourceLabelSymbol((MethodSymbol)this.ContainingMemberOrLambda, node, labelExpressionConstant);
            }

            return new BoundSwitchLabel(
                syntax: node,
                label: boundLabelSymbol,
                expressionOpt: boundLabelExpressionOpt,
                constantValueOpt: labelExpressionConstant,
                hasErrors: hasErrors || hasDuplicateErrors);
        }

        internal virtual BoundStatement BindGotoCaseOrDefault(GotoStatementSyntax node, Binder gotoBinder, DiagnosticBag diagnostics)
        {
            Debug.Assert(node.Kind() == SyntaxKind.GotoCaseStatement || node.Kind() == SyntaxKind.GotoDefaultStatement);
            BoundExpression gotoCaseExpressionOpt = null;

            // Prevent cascading diagnostics
            if (!node.HasErrors)
            {
                ConstantValue gotoCaseExpressionConstant = null;
                TypeSymbol switchGoverningType = SwitchGoverningType;
                bool hasErrors = false;
                List<SourceLabelSymbol> matchedLabelSymbols;

                // SPEC:    If the goto case statement is not enclosed by a switch statement, if the constant-expression
                // SPEC:    is not implicitly convertible (§6.1) to the governing type of the nearest enclosing switch statement,
                // SPEC:    or if the nearest enclosing switch statement does not contain a case label with the given constant value,
                // SPEC:    a compile-time error occurs.

                // SPEC:    If the goto default statement is not enclosed by a switch statement, or if the nearest enclosing
                // SPEC:    switch statement does not contain a default label, a compile-time error occurs.

                if (node.Expression != null)
                {
                    Debug.Assert(node.Kind() == SyntaxKind.GotoCaseStatement);

                    // Bind the goto case expression
                    gotoCaseExpressionOpt = gotoBinder.BindValue(node.Expression, diagnostics, BindValueKind.RValue);

                    gotoCaseExpressionOpt = ConvertCaseExpression(switchGoverningType, node, gotoCaseExpressionOpt, gotoBinder,
                        ref gotoCaseExpressionConstant, diagnostics, isGotoCaseExpr: true);

                    // Check for bind errors
                    hasErrors = hasErrors || gotoCaseExpressionOpt.HasAnyErrors;

                    if (!hasErrors && gotoCaseExpressionConstant == null)
                    {
                        diagnostics.Add(ErrorCode.ERR_ConstantExpected, node.Location);
                        hasErrors = true;
                    }

                    // LabelSymbols for all the switch case labels are created by BuildLabels().
                    // Fetch the matching switch case label symbols
                    matchedLabelSymbols = FindMatchingSwitchCaseLabels(gotoCaseExpressionConstant, node);
                }
                else
                {
                    Debug.Assert(node.Kind() == SyntaxKind.GotoDefaultStatement);
                    matchedLabelSymbols = GetDefaultLabels();
                }

                if (matchedLabelSymbols.IsEmpty())
                {
                    if (!hasErrors)
                    {
                        // No matching case label/default label found
                        var labelName = SyntaxFacts.GetText(node.CaseOrDefaultKeyword.Kind());
                        if (node.Kind() == SyntaxKind.GotoCaseStatement)
                        {
                            labelName += " " + gotoCaseExpressionConstant.Value?.ToString();
                        }
                        labelName += ":";

                        diagnostics.Add(ErrorCode.ERR_LabelNotFound, node.Location, labelName);
                        hasErrors = true;
                    }
                }
                else
                {
                    return new BoundGotoStatement(node, matchedLabelSymbols[0], gotoCaseExpressionOpt, null, hasErrors);
                }
            }

            return new BoundBadStatement(
                syntax: node,
                childBoundNodes: gotoCaseExpressionOpt != null ? ImmutableArray.Create<BoundNode>(gotoCaseExpressionOpt) : ImmutableArray<BoundNode>.Empty,
                hasErrors: true);
        }

        #endregion
    }
}
