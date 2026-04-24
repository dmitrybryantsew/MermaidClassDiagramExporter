# Publishing Checklist

## Before Pushing

- verify Unity imports the plugin cleanly in a fresh test project
- confirm `Tools > Mermaid` menu items appear
- confirm `Open Type Graph Viewer` opens without compile errors
- confirm export works and writes Mermaid output
- confirm the bundled DLLs import as editor plugins, not analyzers

## Before Making A Release

- review `THIRD_PARTY_NOTICES.md`
- review `LICENSE`
- update `CHANGELOG.md`
- keep the WIP/prototype note in `README.md` accurate
- add screenshots to the root `README.md` if desired
- tag the release, for example `v0.1.0`

## Nice Next Steps

- add assembly definition files
- make export path configurable
- add a proper Unity Package Manager `package.json`
- add a sample graph screenshot or demo scene
- test on one or two more Unity versions
