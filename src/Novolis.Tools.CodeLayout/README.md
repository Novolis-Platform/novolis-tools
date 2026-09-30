# Novolis.Tools.CodeLayout

Source-layout mapping and repair APIs used by `novolis-code-layout`.

The library scans the compile items of every project in an `.slnx`, identifies
files with multiple top-level types, classifies type-less files, and applies the
same type split used by the command-line tool. Deletion is limited to explicit
empty or comment-only candidates and is opt-in at the CLI.
