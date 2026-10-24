# CLAUDE.md

- Every file uses CRLF line endings. Never use `sed -i`; it silently converts files to LF.
- Generated files (for example `python/uv.lock`) keep their tool's line endings; `.gitattributes` lists them.
- No comments: no `//`, `///` or docstrings unless truly unavoidable. Rationale goes in commit messages and docs.
- Write-ups are concise: ordered bullets, one-line justifications.
- No silent failures: never swallow errors or return null or defaults to stay "safe". Fail loudly.
- No just-in-case fields: delete anything without a current reader.
- Every enum's first member (value 0) is `NotSet`.

Code structure:
- Design before code: for anything non-trivial, first say which types exist, what each is responsible for, and what the change replaces; agree it, then write it.
- When something new does not fit, reshape the structure rather than bolt it on. Warning signs: a special case, a flag, a nullable that means "not applicable", a method that throws for some instances, a parameter some implementations ignore, a type check.
- One concept in one place, named for what it is (no Helpers, Utils, Manager, or vague plurals like Families).
- An abstraction must earn its place: no interface or generic indirection without a second real use, and none that makes the code harder to follow top to bottom.
- Little duplication, within reason; across languages, the contract and parity tests carry it.
- After a piece works, reread it as a reviewer would: what would you delete, merge or rename? Do that before presenting it.
