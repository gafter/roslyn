# Pattern-Matching Extensions post C# 7.

### Specification
  - [ ] Remove spec from Roslyn and link to spec in csharplang
  - [ ] Need a specified resolution to the ambiguity in a pattern like `( 2 )`
  - [ ] Update the spec to use `:` in a property pattern, e.g. `p is { X: 2 }`
  - [ ] Clarify the kind of "binding failure" which helps resolve the type-constant ambiguity in `e is X`

### Language Design Issues
  - [ ] Are we going to combine forms, e.g. `Type ( Pat, Pat ) { Name : Pat } identifier` where
    - At least one of `(`, `identifier`, or `{` is required
    - Type may be omitted if at least one of the `(` or `{` part is included
    - It is an error if an `Type` is omitted and `identifier` is present and `ITuple` is used to match a tuple-like pattern.

### Parser
- [ ] Resurrect the prototype parser for recursive patterns - how much can be used?
- [ ] Write an extensive set of tests for parsing recursive patterns.
- [ ] What next?

### Specific tests
- All of the possible recursive pattern form
- [ ] `Type (-part`
- [ ] `Type {-part`
- [ ] `Type (-part {-part`
- [ ] `Type identifier`
- [ ] `Type (-part identifier `
- [ ] `Type {-part identifier `
- [ ] `Type (-part {-part identifier `
- [ ] `(-part`
- [ ] `{-part`
- [ ] `(-part {-part`
- [ ] `(-part identifier`
- [ ] `{-part identifier`
- [ ] `(-part {-part identifier`
- All of the possible `(` parts
- [ ] `( Identifier: Pattern )`
- [ ] `( Pattern, Pattern )`
- [ ] `( Identifier: Pattern, Pattern )`
- [ ] `( Pattern, Identifier: Pattern )`
- [ ] `( Pattern )` if a {-part or an identifier is present.
- All of the possible `{` parts
- [ ] `{ }`
- [ ] `{ Identifier: Pattern }`
- [ ] `{ Identifier: Pattern, Identifier: Pattern }`
- [ ] `{ [ constant-expression ]: Pattern }`
