// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    // We use a subclass of SwitchBinder for the pattern-matching switch statement until we have completed
    // a totally compatible implementation of switch that also accepts pattern-matching constructs.
    internal partial class PatternSwitchBinder : SwitchBinder
    {
        private bool? _isPatternSwitch;

        internal PatternSwitchBinder(Binder next, SwitchStatementSyntax switchSyntax) : base(next, switchSyntax)
        {
        }

        private bool IsPatternSwitch
        {
            get
            {
                if (_isPatternSwitch == null)
                {
                    var parseOptions = _switchSyntax?.SyntaxTree?.Options as CSharpParseOptions;
                    _isPatternSwitch =
                        (parseOptions?.IsFeatureEnabled(MessageID.IDS_FeaturePatternMatching) != false &&
                        (parseOptions?.Features.ContainsKey("typeswitch") == true ||
                         IsPatternSwitchSyntax(_switchSyntax) ||
                         !SwitchGoverningType.IsValidV6SwitchGoverningType()));
                }

                return _isPatternSwitch.GetValueOrDefault();
            }
        }



        //// When pattern-matching is enabled, we use a completely different binder and binding
        //// strategy for switch statements. Once we have confirmed that it is totally upward
        //// compatible with the existing syntax and semantics, we will remove *this* binder
        //// and use the new one for binding all switch statements. However, until we have
        //// edit-and-continue working, we continue using the old binder when we can.

        private static bool IsPatternSwitchSyntax(SwitchStatementSyntax switchSyntax)
        {
            foreach (var section in switchSyntax.Sections)
            {
                if (section.Labels.Any(SyntaxKind.CasePatternSwitchLabel))
                {
                    return true;
                }
            }

            return false;
        }

        internal override BoundStatement BindSwitchExpressionAndSections(SwitchStatementSyntax node, Binder originalBinder, DiagnosticBag diagnostics)
        {
            // If it is a valid C# 6 switch statement, we use the old binder to bind it.
            if (!IsPatternSwitch) return base.BindSwitchExpressionAndSections(node, originalBinder, diagnostics);

            Debug.Assert(_switchSyntax.Equals(node));

            // Bind switch expression and set the switch governing type.
            var boundSwitchExpression = SwitchGoverningExpression;
            diagnostics.AddRange(SwitchGoverningDiagnostics);

            BoundPatternSwitchLabel defaultLabel;
            ImmutableArray<BoundPatternSwitchSection> switchSections = BindPatternSwitchSections(boundSwitchExpression, node.Sections, originalBinder, out defaultLabel, diagnostics);
            var locals = GetDeclaredLocalsForScope(node);
            var functions = GetDeclaredLocalFunctionsForScope(node);
            return new BoundPatternSwitchStatement(
                node, boundSwitchExpression,
                locals, functions, switchSections, defaultLabel, this.BreakLabel, this);
        }

        private SourceLabelSymbol GetDefaultLabel()
        {
            // SwitchLabelsMap: Dictionary for the switch case/default labels.
            // Default label(s) are indexed on a special DefaultKey object.

            return FindMatchingSwitchLabel(s_defaultKey);
        }

        private static readonly object s_nullKey = new object();
        private static object KeyForConstant(ConstantValue constantValue)
        {
            Debug.Assert(constantValue != (object)null);
            return constantValue.IsNull ? s_nullKey : constantValue.Value;
        }

        private SourceLabelSymbol FindMatchingSwitchCaseLabel(ConstantValue constantValue, CSharpSyntaxNode labelSyntax)
        {
            // SwitchLabelsMap: Dictionary for the switch case/default labels.
            // Case labels with a non-null constant value are indexed on their ConstantValue.
            // Invalid case labels (with null constant value) are indexed on the label syntax.

            object key;
            if (constantValue != (object)null && !constantValue.IsBad)
            {
                key = KeyForConstant(constantValue);
            }
            else
            {
                key = labelSyntax;
            }

            return FindMatchingSwitchLabel(key);
        }

        private SourceLabelSymbol FindMatchingSwitchLabel(object key)
        {
            Debug.Assert(key != null);

            var labelsMap = LabelsByValue;
            if (labelsMap != null)
            {
                SourceLabelSymbol label;
                if (labelsMap.TryGetValue(key, out label))
                {
                    Debug.Assert(label != (object)null);
                    return label;
                }
            }

            return null;
        }

        // Dictionary for the switch case/default labels.
        // Case labels with a non-null constant value are indexed on their ConstantValue.
        // Default label(s) are indexed on a special DefaultKey object.
        // Invalid case labels with null constant value are indexed on the labelName.
        private Dictionary<object, SourceLabelSymbol> _lazySwitchLabelsMap;
        private static readonly object s_defaultKey = new object();

        private Dictionary<object, SourceLabelSymbol> LabelsByValue
        {
            get
            {
                if (_lazySwitchLabelsMap == null && this.Labels.Length > 0)
                {
                    _lazySwitchLabelsMap = BuildLabelsByValue(this.Labels);
                }

                return _lazySwitchLabelsMap;
            }
        }

        private static Dictionary<object, SourceLabelSymbol> BuildLabelsByValue(ImmutableArray<LabelSymbol> labels)
        {
            Debug.Assert(labels.Length > 0);

            var map = new Dictionary<object, SourceLabelSymbol>(labels.Length, new SwitchConstantValueHelper.SwitchLabelsComparer());
            foreach (SourceLabelSymbol label in labels)
            {
                SyntaxKind labelKind = label.IdentifierNodeOrToken.Kind();

                if (labelKind == SyntaxKind.CaseSwitchLabel ||
                    labelKind == SyntaxKind.DefaultSwitchLabel)
                {
                    object key;
                    var constantValue = label.SwitchCaseLabelConstant;
                    if (constantValue != (object)null && !constantValue.IsBad)
                    {
                        // Case labels with a non-null constant value are indexed on their ConstantValue.
                        key = KeyForConstant(constantValue);
                    }
                    else if (labelKind == SyntaxKind.DefaultSwitchLabel)
                    {
                        // Default label(s) are indexed on a special DefaultKey object.
                        key = s_defaultKey;
                    }
                    else
                    {
                        // Invalid case labels with null constant value are indexed on the labelName.
                        key = label.IdentifierNodeOrToken.AsNode();
                    }

                    if (!map.ContainsKey(key))
                    {
                        map.Add(key, label);
                    }
                    else
                    {
                        // If there is a duplicate label, ignore it. It will be reported when binding the switch label.
                    }
                }
            }

            return map;
        }

        private ImmutableArray<BoundPatternSwitchSection> BindPatternSwitchSections(BoundExpression boundSwitchExpression, SyntaxList<SwitchSectionSyntax> sections, Binder originalBinder, out BoundPatternSwitchLabel defaultLabel, DiagnosticBag diagnostics)
        {
            defaultLabel = null;

            // Bind match sections
            var boundPatternSwitchSectionsBuilder = ArrayBuilder<BoundPatternSwitchSection>.GetInstance();
            foreach (var sectionSyntax in sections)
            {
                boundPatternSwitchSectionsBuilder.Add(BindPatternSwitchSection(boundSwitchExpression, sectionSyntax, originalBinder, ref defaultLabel, diagnostics));
            }

            return boundPatternSwitchSectionsBuilder.ToImmutableAndFree();
        }

        private BoundPatternSwitchSection BindPatternSwitchSection(
            BoundExpression boundSwitchExpression,
            SwitchSectionSyntax node,
            Binder originalBinder,
            ref BoundPatternSwitchLabel defaultLabel,
            DiagnosticBag diagnostics)
        {
            // Bind match section labels
            var boundLabelsBuilder = ArrayBuilder<BoundPatternSwitchLabel>.GetInstance();
            var sectionBinder = originalBinder.GetBinder(node); // this binder can bind pattern variables from the section.
            Debug.Assert(sectionBinder != null);
            var labelsByNode = LabelsByNode;

            foreach (var labelSyntax in node.Labels)
            {
                LabelSymbol label = labelsByNode[labelSyntax];
                BoundPatternSwitchLabel boundLabel = BindPatternSwitchSectionLabel(sectionBinder, boundSwitchExpression, labelSyntax, label, ref defaultLabel, diagnostics);
                boundLabelsBuilder.Add(boundLabel);
            }

            // Bind switch section statements
            var boundStatementsBuilder = ArrayBuilder<BoundStatement>.GetInstance();
            foreach (var statement in node.Statements)
            {
                boundStatementsBuilder.Add(sectionBinder.BindStatement(statement, diagnostics));
            }

            return new BoundPatternSwitchSection(node, sectionBinder.GetDeclaredLocalsForScope(node), boundLabelsBuilder.ToImmutableAndFree(), boundStatementsBuilder.ToImmutableAndFree());
        }

        private BoundPatternSwitchLabel BindPatternSwitchSectionLabel(
            Binder sectionBinder, BoundExpression boundSwitchExpression, SwitchLabelSyntax node, LabelSymbol label, ref BoundPatternSwitchLabel defaultLabel, DiagnosticBag diagnostics)
        {
            switch (node.Kind())
            {
                case SyntaxKind.CaseSwitchLabel:
                    {
                        var caseLabelSyntax = (CaseSwitchLabelSyntax)node;
                        bool wasExpression;
                        var pattern = sectionBinder.BindConstantPattern(
                            node, boundSwitchExpression, boundSwitchExpression.Type, caseLabelSyntax.Value, node.HasErrors, diagnostics, out wasExpression, wasSwitchCase: true);
                        bool hasErrors = pattern.HasErrors;
                        var constantValue = pattern.ConstantValue;
                        if (!hasErrors && constantValue != (object)null && this.FindMatchingSwitchCaseLabel(constantValue, caseLabelSyntax) != label)
                        {
                            diagnostics.Add(ErrorCode.ERR_DuplicateCaseLabel, node.Location, pattern.ConstantValue.GetValueToDisplay() ?? label.Name);
                            hasErrors = true;
                        }
                        return new BoundPatternSwitchLabel(node, label, pattern, null, hasErrors);
                    }

                case SyntaxKind.DefaultSwitchLabel:
                    {
                        var defaultLabelSyntax = (DefaultSwitchLabelSyntax)node;
                        var pattern = new BoundWildcardPattern(node);
                        bool hasErrors = pattern.HasErrors;
                        if (defaultLabel != null)
                        {
                            diagnostics.Add(ErrorCode.ERR_DuplicateCaseLabel, node.Location, "default");
                            hasErrors = true;
                        }

                        // Note that this is semantically last! The caller will place it in the decision tree
                        // in the final position.
                        defaultLabel = new BoundPatternSwitchLabel(node, label, pattern, null, hasErrors);
                        return defaultLabel;
                    }

                case SyntaxKind.CasePatternSwitchLabel:
                    {
                        var matchLabelSyntax = (CasePatternSwitchLabelSyntax)node;
                        var pattern = sectionBinder.BindPattern(
                            matchLabelSyntax.Pattern, boundSwitchExpression, boundSwitchExpression.Type, node.HasErrors, diagnostics, wasSwitchCase: true);
                        return new BoundPatternSwitchLabel(node, label, pattern,
                            matchLabelSyntax.WhenClause != null ? sectionBinder.BindBooleanExpression(matchLabelSyntax.WhenClause.Condition, diagnostics) : null, node.HasErrors);
                    }

                default:
                    throw ExceptionUtilities.UnexpectedValue(node);
            }
        }

        private Dictionary<SyntaxNode, LabelSymbol> _labelsByNode;
        private Dictionary<SyntaxNode, LabelSymbol> LabelsByNode
        {
            get
            {
                if (_labelsByNode == null)
                {
                    var result = new Dictionary<SyntaxNode, LabelSymbol>();
                    foreach (var label in Labels)
                    {
                        var node = ((SourceLabelSymbol)label).IdentifierNodeOrToken.AsNode();
                        if (node != null)
                        {
                            result.Add(node, label);
                        }
                    }
                    _labelsByNode = result;
                }

                return _labelsByNode;
            }
        }

        protected override ImmutableArray<LabelSymbol> BuildLabels()
        {
            if (!IsPatternSwitch) return base.BuildLabels();

            // We bind the switch expression and the switch case label expressions so that the constant values can be
            // part of the label, but we do not report any diagnostics here. Diagnostics will be reported during binding.

            ArrayBuilder <LabelSymbol> labels = ArrayBuilder<LabelSymbol>.GetInstance();
            DiagnosticBag tempDiagnosticBag = DiagnosticBag.GetInstance();
            TypeSymbol switchGoverningType = this.SwitchGoverningType;
            foreach (var section in _switchSyntax.Sections)
            {
                // add switch case/default labels
                BuildSwitchLabels(switchGoverningType, section.Labels, GetBinder(section), labels, tempDiagnosticBag);

                // add regular labels from the statements in the switch section
                BuildLabels(section.Statements, ref labels);
            }

            tempDiagnosticBag.Free();
            return labels.ToImmutableAndFree();
        }

        private void BuildSwitchLabels(TypeSymbol switchGoverningType, SyntaxList<SwitchLabelSyntax> labelsSyntax, Binder sectionBinder, ArrayBuilder<LabelSymbol> labels, DiagnosticBag tempDiagnosticBag)
        {
            // add switch case/default labels
            foreach (var labelSyntax in labelsSyntax)
            {
                ConstantValue boundLabelConstantOpt = null;
                switch (labelSyntax.Kind())
                {
                    case SyntaxKind.CaseSwitchLabel:
                        // compute the constant value to place in the label symbol
                        var caseLabel = (CaseSwitchLabelSyntax)labelSyntax;
                        Debug.Assert(caseLabel.Value != null);
                        var boundLabelExpression = sectionBinder.BindValue(caseLabel.Value, tempDiagnosticBag, BindValueKind.RValue);
                        boundLabelExpression = ConvertCaseExpression(switchGoverningType, labelSyntax, boundLabelExpression, sectionBinder, ref boundLabelConstantOpt, tempDiagnosticBag);
                        break;

                    default:
                        // No constant value
                        break;
                }

                // Create the label symbol
                labels.Add(new SourceLabelSymbol((MethodSymbol)this.ContainingMemberOrLambda, labelSyntax, boundLabelConstantOpt));
            }
        }

        internal override BoundStatement BindGotoCaseOrDefault(GotoStatementSyntax node, Binder gotoBinder, DiagnosticBag diagnostics)
        {
            if (!IsPatternSwitch) return base.BindGotoCaseOrDefault(node, gotoBinder, diagnostics);

            Debug.Assert(node.Kind() == SyntaxKind.GotoCaseStatement || node.Kind() == SyntaxKind.GotoDefaultStatement);
            BoundExpression gotoCaseExpressionOpt = null;

            // Prevent cascading diagnostics
            if (!node.HasErrors)
            {
                ConstantValue gotoCaseExpressionConstant = null;
                TypeSymbol switchGoverningType = SwitchGoverningType;
                bool hasErrors = false;
                SourceLabelSymbol matchedLabelSymbol;

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
                    matchedLabelSymbol = FindMatchingSwitchCaseLabel(gotoCaseExpressionConstant, node);
                }
                else
                {
                    Debug.Assert(node.Kind() == SyntaxKind.GotoDefaultStatement);
                    matchedLabelSymbol = GetDefaultLabel();
                }

                if (matchedLabelSymbol == (object)null)
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
                    return new BoundGotoStatement(node, matchedLabelSymbol, gotoCaseExpressionOpt, null, hasErrors);
                }
            }

            return new BoundBadStatement(
                syntax: node,
                childBoundNodes: gotoCaseExpressionOpt != null ? ImmutableArray.Create<BoundNode>(gotoCaseExpressionOpt) : ImmutableArray<BoundNode>.Empty,
                hasErrors: true);
        }

    }
}
