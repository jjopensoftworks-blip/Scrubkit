<!-- Thanks for contributing! Please keep PRs focused. -->

## What & why

Briefly describe the change and the motivation. Link any related issue (e.g. `Fixes #123`).

## Checklist

- [ ] `dotnet build Scrubkit.slnx -c Release` passes (no warnings — Release is warnaserror)
- [ ] `dotnet test Scrubkit.slnx -c Release` passes; new code is covered
- [ ] Public API changes are documented with XML doc comments
- [ ] Kept the Abstractions boundary (no heavy deps in `Scrubkit.Abstractions`)
- [ ] If dependencies changed: updated `Directory.Packages.props` and committed refreshed
      `packages.lock.json`
- [ ] Updated `CHANGELOG.md` under `[Unreleased]` if user-facing
