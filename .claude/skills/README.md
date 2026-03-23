# Claude Code Skills

## What are Skills?

Skills are markdown files in `.claude/skills/` that provide Claude Code with project-specific knowledge. They are automatically loaded as context when working in this repository, helping Claude make decisions consistent with the codebase's actual patterns and conventions.

## Available Skills

| Skill | File | Purpose |
|---|---|---|
| **Angular Patterns** | `angular-patterns.md` | Component structure, NgRx store conventions, DI patterns, routing, forms, testing |
| **Design System** | `design-system.md` | Styling stack (Tailwind + Material + SCSS), color palette, typography, UI components |
| **Project-Specific** | `project-specific.md` | Domain model, platform integration, config-driven HTTP, schema-driven forms, deployment |
| **Schema Authoring** | `schema-authoring.md` | JSON schema authoring for sections, blocks, templates, shared settings, objects |
| **AngularJS Legacy** | `angularjs-legacy.md` | Rules and architecture for the legacy AngularJS admin integration (`Web/Scripts/`) |

## When Each Skill is Used

### angular-patterns.md
- Creating new components, services, or modules
- Adding or modifying NgRx store features (actions/effects/reducers/selectors)
- Working with reactive forms or dynamic form system
- Writing or fixing tests
- Routing changes

### design-system.md
- Styling components (choosing Tailwind vs SCSS)
- Using Material components
- Referencing color tokens or typography variables
- Layout and positioning decisions

### project-specific.md
- Working with API integration or HttpClient
- Platform communication (postMessage, BroadcastChannel)
- Template/section/block CRUD operations
- Understanding business domain concepts
- Build and deployment questions

### schema-authoring.md
- Creating or editing section/block descriptor JSON files
- Defining templates (`schemas/templates/`)
- Working with shared settings or object types
- Writing visibility expressions or ServerRequestDescriptor in schemas

### angularjs-legacy.md
- Reading or making minimal changes to `Web/Scripts/`
- Questions about admin blade UI or widget behavior
- File upload handler or `$resource` API wrapper

## How to Update Skills

1. Edit the relevant `.md` file in `.claude/skills/`
2. Keep content based on **real code patterns** — not generic best practices
3. Update examples when the codebase patterns change
4. Remove sections that no longer apply

## Adding a New Skill

1. Create a new `.md` file in `.claude/skills/`
2. Follow the structure: `# Title` -> `## Description` -> `## Triggers` -> content sections
3. Add it to this README's table
4. Focus on patterns that require reading multiple files to understand — don't document what's obvious from a single file
