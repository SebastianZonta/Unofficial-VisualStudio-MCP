# 06: Ship the skills with the installer

**What to build:** A developer runs the one-line installer and gets the server *and*
the three flow-testing skills in every client it wires, version-locked to the exe
(ADR-0014):

- the release zip carries the skills;
- both installers install them into the folders probe 05 chose, replacing only the
  `vs-debug-*` skill folders they own and never touching other skills;
- the setup skill carries each client's user-only flag as found by probe 05;
- the CI install guard catches a dropped skill;
- the docs tell developers about it.

Spec: [Packaging and distribution](../spec.md#packaging-and-distribution-adr-0014),
[Docs in this repo](../spec.md#docs-in-this-repo).

**Blocked by:** 03 (Plan a launch-triggered flow), 05 (Probe: skill folders, duplicates and user-only flags per client)

**Status:** ready-for-agent

- [ ] The release workflow packs the skills folder into the release zip.
- [ ] A full install and a config-only run both install every skill into each wired client's folders per probe 05, and each skill lists exactly once per client.
- [ ] A re-run replaces only the `vs-debug-*` skill folders; a skill the developer installed separately is untouched.
- [ ] Claude Desktop gets no skills, and the installer output says why.
- [ ] Each client gets the user-only flag probe 05 found for setup; where none exists, setup's description still says user-only.
- [ ] CI install guard: it fails when the zip step or either installer stops covering any skill folder.
- [ ] INSTALL states which folders the installer writes outside its install folder. The README gains a Skills section (what the three do, installed by the one-liner). The `npx skills add` route is documented with the warning that mixing channels duplicates skills.
