# 01: Seam contract plus fake for module listing

**What to build:** a read-only session-scoped module-list operation on the seam with fake-seeded module tables (loaded/missing/failed symbol states), so agents can diagnose unbound breakpoints with no VS.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Listing returns name, path, loaded flag, symbol state, and detail, ordered deterministically by name.
- [x] Name filter works; one bad entry is flagged without failing the list; design mode fails with not-debugging.
- [x] Modules are scoped per debug session; listing twice changes nothing observable.
