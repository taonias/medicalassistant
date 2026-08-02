# Issue tracker: Local Markdown

Issues and PRDs for this repository live as Markdown files under `.scratch/`. External pull requests are not a triage request surface.

## Conventions

- One feature per directory: `.scratch/<feature-slug>/`
- The PRD is `.scratch/<feature-slug>/PRD.md`
- Implementation issues are `.scratch/<feature-slug>/issues/<NN>-<slug>.md`, numbered from `01`
- Triage state is recorded as a `Status:` line near the top of each issue file; see [triage-labels.md](triage-labels.md) for accepted values
- Comments and conversation history append under a `## Comments` heading

## When a skill says "publish to the issue tracker"

Create the appropriate Markdown file beneath `.scratch/<feature-slug>/`, creating the feature directory if needed.

## When a skill says "fetch the relevant ticket"

Read the referenced local Markdown path. The user will normally provide the path or local issue number.
