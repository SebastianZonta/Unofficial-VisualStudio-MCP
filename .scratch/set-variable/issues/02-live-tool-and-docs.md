# 02: Live EnvDTE write plus tool and docs

**What to build:** the live bridge writes one variable per consent-carrying call and a `set_variable` tool exposes it, verified manually by read-write-reread.

**Blocked by:** 01 (needs the seam contract to implement).

**Status:** ready-for-human (code and docs done; live check pending: E2E W1–W10 against real VS)

- [ ] Live attach, break, read, write with consent, re-read confirms, resume shows the effect.
- [x] Consent is per call and never shared with attach consent; strict coercion, no silent truncation.
- [x] `TOOLS.md`, `GLOSSARY.md`, and `README.md` document the tool and its consent model (no test counts in `README.md`).
