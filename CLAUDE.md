# CLAUDE.md

- Every file uses CRLF line endings. Never use `sed -i`; it silently converts files to LF.
- No comments: no `//`, `///` or docstrings unless truly unavoidable. Rationale goes in commit messages and docs.
- Write-ups are concise: ordered bullets, one-line justifications.
- No silent failures: never swallow errors or return null or defaults to stay "safe". Fail loudly.
- No just-in-case fields: delete anything without a current reader.
- Every enum's first member (value 0) is `NotSet`.
