# Changelog

## [v1.1.2](https://github.com/devlooped/StructId/tree/v1.1.2) (2026-04-15)

[Full Changelog](https://github.com/devlooped/StructId/compare/v1.1.1...v1.1.2)

## [v1.1.1](https://github.com/devlooped/StructId/tree/v1.1.1) (2026-04-15)

[Full Changelog](https://github.com/devlooped/StructId/compare/v1.1.0...v1.1.1)

:sparkles: Implemented enhancements:

- Update Guid-backed ID factory to use CreateVersion7 on .NET 9+ [\#106](https://github.com/devlooped/StructId/pull/106) (@kzu)
- Add SKILL.md and auto-copy to consuming repos [\#99](https://github.com/devlooped/StructId/pull/99) (@kzu)

:hammer: Other:

- Create comprehensive project design and implementation documentation in AGENTS.md [\#97](https://github.com/devlooped/StructId/issues/97)

:twisted_rightwards_arrows: Merged:

- Fallback to SolutionDir when git root is unavailable for skill copy [\#100](https://github.com/devlooped/StructId/pull/100) (@Copilot)
- Add comprehensive AGENTS.md implementation reference [\#98](https://github.com/devlooped/StructId/pull/98) (@Copilot)

## [v1.1.0](https://github.com/devlooped/StructId/tree/v1.1.0) (2026-04-14)

[Full Changelog](https://github.com/devlooped/StructId/compare/v1.0.0-rc...v1.1.0)

:sparkles: Implemented enhancements:

- All StructId generators are not incremental and extremely inefficient \(analyzers too\) [\#60](https://github.com/devlooped/StructId/issues/60)
- Trim package dependencies significantly [\#89](https://github.com/devlooped/StructId/pull/89) (@kzu)
- Fix incremental source generation and add incrementality tests [\#82](https://github.com/devlooped/StructId/pull/82) (@kzu)
- Improve record analyzer performance [\#61](https://github.com/devlooped/StructId/pull/61) (@kzu)

:bug: Fixed bugs:

- CA1813 [\#80](https://github.com/devlooped/StructId/issues/80)
- Having some issues with IStructId\<char\> [\#79](https://github.com/devlooped/StructId/issues/79)
- Template TSelf contraint does not seem to work [\#75](https://github.com/devlooped/StructId/issues/75)
- Fix Analyser warning [\#72](https://github.com/devlooped/StructId/issues/72)
- Fix Dapper generator encoding issue on macOS [\#94](https://github.com/devlooped/StructId/pull/94) (@kzu)

:hammer: Other:

- Filter which templates are applied [\#81](https://github.com/devlooped/StructId/issues/81)
- Switch to ByteAether.Ulid for faster, always‑reliable ULID generation [\#78](https://github.com/devlooped/StructId/issues/78)
- Value Validator feature request [\#77](https://github.com/devlooped/StructId/issues/77)
- Opt out of implicit operators [\#74](https://github.com/devlooped/StructId/issues/74)
- Template question [\#73](https://github.com/devlooped/StructId/issues/73)
- :\) [\#59](https://github.com/devlooped/StructId/issues/59)

:twisted_rightwards_arrows: Merged:

- Target latest .NET LTS from tests [\#90](https://github.com/devlooped/StructId/pull/90) (@kzu)
- Adopt OSMF [\#87](https://github.com/devlooped/StructId/pull/87) (@kzu)
- Provide some additional package tags for discoverability [\#58](https://github.com/devlooped/StructId/pull/58) (@kzu)

## [v1.0.0-rc](https://github.com/devlooped/StructId/tree/v1.0.0-rc) (2024-12-23)

[Full Changelog](https://github.com/devlooped/StructId/compare/v1.0.0...v1.0.0-rc)

## [v1.0.0](https://github.com/devlooped/StructId/tree/v1.0.0) (2024-12-23)

[Full Changelog](https://github.com/devlooped/StructId/compare/v1.0.0-beta...v1.0.0)

:sparkles: Implemented enhancements:

- Annotate compiled templates with NuGetPackageId metadata [\#57](https://github.com/devlooped/StructId/pull/57) (@kzu)
- Add legacy TypeConverter support from System.ComponentModel [\#55](https://github.com/devlooped/StructId/pull/55) (@kzu)
- Drastically simply packing and namespace management [\#53](https://github.com/devlooped/StructId/pull/53) (@kzu)
- Improve detection of IStructId namespace from compilation [\#52](https://github.com/devlooped/StructId/pull/52) (@kzu)

:bug: Fixed bugs:

- Fix issue when writing custom values to STJ [\#54](https://github.com/devlooped/StructId/pull/54) (@kzu)

:twisted_rightwards_arrows: Merged:

- Allow running samples from main solution for debugging [\#56](https://github.com/devlooped/StructId/pull/56) (@kzu)
- Further improve docs and comment code for posterity [\#51](https://github.com/devlooped/StructId/pull/51) (@kzu)

## [v1.0.0-beta](https://github.com/devlooped/StructId/tree/v1.0.0-beta) (2024-12-21)

[Full Changelog](https://github.com/devlooped/StructId/compare/v1.0.0-alpha...v1.0.0-beta)

## [v1.0.0-alpha](https://github.com/devlooped/StructId/tree/v1.0.0-alpha) (2024-12-21)

[Full Changelog](https://github.com/devlooped/StructId/compare/v0.1.0...v1.0.0-alpha)

:sparkles: Implemented enhancements:

- Consider adding Ulid wrapper. [\#18](https://github.com/devlooped/StructId/issues/18)
- Make feature-dependent templates conditional [\#46](https://github.com/devlooped/StructId/pull/46) (@kzu)
- Add built-in support for parsable/formattable values for EF [\#43](https://github.com/devlooped/StructId/pull/43) (@kzu)
- Generalize value templates too, automatically adding Ulid to Dapper [\#40](https://github.com/devlooped/StructId/pull/40) (@kzu)
- Showcase extensibility by leveraging Ulid [\#39](https://github.com/devlooped/StructId/pull/39) (@kzu)
- Preserve trivia when applying code templates [\#38](https://github.com/devlooped/StructId/pull/38) (@kzu)
- Report and fix ALL partials of template without file-local modifier [\#37](https://github.com/devlooped/StructId/pull/37) (@kzu)
- Create analyzer and codefix for templates [\#35](https://github.com/devlooped/StructId/pull/35) (@kzu)
- Split SpanFormattable from Formattable [\#34](https://github.com/devlooped/StructId/pull/34) (@kzu)
- Fix improper type names in sample templates [\#32](https://github.com/devlooped/StructId/pull/32) (@kzu)
- Turn implicit conversion into compiled templates [\#31](https://github.com/devlooped/StructId/pull/31) (@kzu)
- Remove unused `[TStructId<T>]` attribute [\#30](https://github.com/devlooped/StructId/pull/30) (@kzu)
- Make IComparable\<T\> a compiled template [\#29](https://github.com/devlooped/StructId/pull/29) (@kzu)
- Add IUtf8SpanFormattable template [\#27](https://github.com/devlooped/StructId/pull/27) (@kzu)
- Introduce first built-in compiled template implementation, ISpanFormattable [\#24](https://github.com/devlooped/StructId/pull/24) (@kzu)
- Showcase generic templates applying to all struct ids [\#22](https://github.com/devlooped/StructId/pull/22) (@kzu)
- Initial user templates support [\#21](https://github.com/devlooped/StructId/pull/21) (@kzu)
- Refactor ctor and implicit conversions as template-based [\#19](https://github.com/devlooped/StructId/pull/19) (@kzu)
- Add ISpanParsable\<TSelf\> implementation [\#17](https://github.com/devlooped/StructId/pull/17) (@kzu)
- Add TId.New\(\) for guid-based ids, implicit and explicit conversions to underlying type [\#13](https://github.com/devlooped/StructId/pull/13) (@kzu)
- Add support for Dapper [\#12](https://github.com/devlooped/StructId/pull/12) (@kzu)
- Rename package to StrucTId, TValue \> TId [\#11](https://github.com/devlooped/StructId/pull/11) (@kzu)
- Add support for EF Core [\#10](https://github.com/devlooped/StructId/pull/10) (@kzu)
- Add Newstonsoft.Json converter support [\#8](https://github.com/devlooped/StructId/pull/8) (@kzu)
- Introduce non-generic IStructId for string-backed ids [\#6](https://github.com/devlooped/StructId/pull/6) (@kzu)

:bug: Fixed bugs:

- Add tests for dapper and fix generation of unsupported type [\#28](https://github.com/devlooped/StructId/pull/28) (@kzu)
- Test and fix custom StructId namespace support [\#23](https://github.com/devlooped/StructId/pull/23) (@kzu)

:twisted_rightwards_arrows: Merged:

- Document diagnostics and additional features [\#50](https://github.com/devlooped/StructId/pull/50) (@kzu)
- Add simple dapper+sqlite console app demo [\#48](https://github.com/devlooped/StructId/pull/48) (@kzu)
- Add sample showcasing no namespace is required for ids [\#47](https://github.com/devlooped/StructId/pull/47) (@kzu)
- Switch to lighter handler type detection in dapper [\#45](https://github.com/devlooped/StructId/pull/45) (@kzu)
- Rename TId \> TValue [\#44](https://github.com/devlooped/StructId/pull/44) (@kzu)
- Add new compiled templates documentation [\#36](https://github.com/devlooped/StructId/pull/36) (@kzu)
- Refactor code templates processing, move more to compiled [\#33](https://github.com/devlooped/StructId/pull/33) (@kzu)
- Improve usage of common namespace, more flexible TId processing [\#26](https://github.com/devlooped/StructId/pull/26) (@kzu)
- Rename base class for generators for better semantics [\#20](https://github.com/devlooped/StructId/pull/20) (@kzu)

## [v0.1.0](https://github.com/devlooped/StructId/tree/v0.1.0) (2024-11-22)

[Full Changelog](https://github.com/devlooped/StructId/compare/04051d776cc03cf3889e36c86aa2fe4a8b51e307...v0.1.0)



\* *This Changelog was automatically generated by [github_changelog_generator](https://github.com/github-changelog-generator/github-changelog-generator)*
