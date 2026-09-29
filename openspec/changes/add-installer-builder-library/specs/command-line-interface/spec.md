# Spec Delta

## Purpose

Defines how build scripts and CI invoke the builder: its command and arguments, what it writes to stdout and stderr, and its exit codes.

## ADDED Requirements

### Requirement: Build command
The builder SHALL provide a `build` command taking the manifest path as its argument, with options `--output <dir>` (overrides the output directory), `--target <name>` (repeatable; builds only the named targets) and `--verbose` (includes external tool output in the log).

#### Scenario: Build everything
- **WHEN** a user runs `build installer.json`
- **THEN** every target in the manifest is built

#### Scenario: Build selected targets
- **WHEN** a user runs `build installer.json --target windows-x64 --target linux-x64` on a manifest with three targets
- **THEN** only those two targets are built, and the third has status `not-selected` in the result

#### Scenario: Unknown target name
- **WHEN** `--target` names a target that is not in the manifest
- **THEN** nothing is built and the builder exits with code 2, naming the unknown target and listing the valid ones

#### Scenario: Output directory override
- **WHEN** a user runs `build installer.json --output /tmp/out`
- **THEN** artifacts are written to `/tmp/out`

### Requirement: Version option
The builder SHALL print its own version and exit with code 0 when run with `--version`.

#### Scenario: Print version
- **WHEN** a user runs the builder with `--version`
- **THEN** it prints its version to stdout and exits with code 0

### Requirement: Result on stdout, logs on stderr
The `build` command SHALL write only the build result, as a single JSON document, to stdout. Progress messages, step headers and (with `--verbose`) external tool output SHALL go to stderr.

#### Scenario: Result is machine-readable
- **WHEN** a CI step pipes the builder's stdout to a JSON parser
- **THEN** the parser receives exactly one valid JSON document, whatever the build's outcome

#### Scenario: Tool output only when verbose
- **WHEN** a build runs without `--verbose`
- **THEN** stderr shows step headers and errors but not the full output of external tools

### Requirement: Exit codes
The builder SHALL exit with code 0 when every selected target succeeded, code 1 when the manifest was valid but one or more selected targets failed, and code 2 when the arguments or the manifest were invalid and nothing was built. With code 2 the builder SHALL still write a result document containing the manifest-level errors and no targets.

#### Scenario: All targets succeed
- **WHEN** every selected target succeeds
- **THEN** the exit code is 0

#### Scenario: One target fails
- **WHEN** one of three targets fails
- **THEN** the exit code is 1

#### Scenario: Invalid manifest
- **WHEN** the manifest has an unknown property
- **THEN** the exit code is 2 and stdout contains a result with the validation error and no targets

#### Scenario: Missing manifest file
- **WHEN** the manifest path does not exist
- **THEN** the exit code is 2 and the error names the path
