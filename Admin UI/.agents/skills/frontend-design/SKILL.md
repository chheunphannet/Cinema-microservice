---
name: frontend-design
description: Use this skill any time you are designing or building a new UI, landing page, dashboard, app screen, or reshaping an existing one. It prevents generic "AI-looking" slop and enforces distinctive, intentional design. It also requires you to verify your work visually using the project's screenshot tool.
license: Complete terms in LICENSE.txt
---

# Frontend Design

## REQUIRED: Leverage Global Design Skills

This workspace has access to powerful global design intelligence skills. When planning your design system, finding color palettes, or deciding on UI patterns:
- Since this project is built with Astro, you MUST invoke the `astro` skill to ensure you follow all Astro framework best practices, routing rules, and component structures.
- You MUST invoke the `ui-ux-pro-max` skill to search its database of styles, font pairings, layout guidelines, and stack-specific rules (including Astro, React, Tailwind). Use its Python search scripts to generate a `--design-system`.
- If building with Tailwind CSS and shadcn/ui, you should also invoke the `ckm:ui-styling` skill for component standards.
Do not guess or invent generic styles—use these global skills to construct a highly professional foundation before writing any code.

You are the design lead at a studio known for giving every client a visual identity nobody would mistake for anyone else's. The client has already rejected templated, cliché output before — they're paying for a specific point of view, not a safe default. Make deliberate, opinionated choices about palette, typography, and layout that come from *this* brief, not from the most statistically likely output for "a landing page" or "a dashboard."

## Step 1 — Ground the design in real subject matter

Before touching colors or layout, nail down: what is this thing, who is it for, and what's the one job the page/screen has to do? If the brief doesn't say, propose a concrete answer and confirm it. Genuinely distinct design comes from the subject's industry, materials, and vernacular. Build with the brief's real content (real copy, real data, real feature names) throughout, not lorem-ipsum placeholders.

## Step 2 — Know the slop, so you can dodge it

AI-generated design right now clusters hard around a handful of tells. 

**Color & background**
- Warm cream background (~#F4F1EA) + high-contrast serif display + terracotta/clay accent (~#D97757)
- Near-black background with one bright acid-green or vermilion accent
- Purple-to-blue gradient hero (the "generic SaaS" gradient)
- Gradient washes used as pure decoration rather than to communicate anything
- Tinted near-black (#0B0B0B, #111) standing in for true black

**Layout & structure**
- Everything chopped into identical rounded cards, one border-radius applied everywhere regardless of hierarchy, the same soft grey shadow (`rgba(0,0,0,.1)`) under each
- Broadsheet layout with hairline rules, zero border-radius, dense newspaper columns
- Numbered markers (01 / 02 / 03) slapped on content that isn't actually a sequence
- Glassmorphism / frosted-glass panels used as generic texture

**Typography & copy chrome**
- One word in a headline picked out in italic, bold, or a different color for no reason
- ALL-CAPS tracked-out labels above every section ("OUR SERVICES")
- Meta text strings joined with middle dots ("Design · Strategy · Build")
- Labels formatted as "WORD — fragment" with a spaced em dash
- Monospace font for small data labels just because it "looks technical"
- An arrow appended to every link/button ("Learn more →")
- Selling copy instead of plain description ("Unlock the power of—" / "Seamlessly—" / "Elevate your—")

**Motion**
- Fade-and-slide-up entrance on every section as it scrolls into view
- Hover-lift + shadow on every card, applied uniformly, not tied to what's interactive

## Step 3 — Work in two passes: plan, then critique, then build

**Pass 1 — token plan.** Before writing any code, write a compact plan:
- **Color:** 4–6 named hex values that form the core palette (not "primary/secondary/accent").
- **Type:** which typefaces, and what role each one plays. One family is enough; if two, make them clearly distinct.
- **Layout:** a one-sentence concept plus an ASCII wireframe. State the alignment (left/center/justified).
- **Principles:** 2–3 sentences on what makes this specific page unique.

**Pass 2 — critique against the slop list.** Before building, check the plan against Step 2. If it looks like a generic default, revise it and note what you changed and why. Only start writing code once the plan has passed this check.

## Step 4 — Design details worth getting right

- **Hero:** open with the most characteristic thing in the subject's world. "Big number + small label + gradient accent" is the default treatment; only use it if it's genuinely the best option.
- **Type scale:** set a deliberate scale with intentional weights and spacing — not the framework's default sizes.
- **Line length:** keep body text under ~80 characters per line.
- **Visual structure carries meaning:** Borders, dividers, eyebrows, and numbering should encode something about the content, not decorate an otherwise empty layout.
- **Motion budget:** pick at most one orchestrated moment (a single page-load sequence, one reveal). 
- **Restraint:** spend boldness in exactly one place. Keep everything else around it quiet. Before shipping, look for one accessory to remove.
- **Quality floor, quietly met:** responsive down to mobile, visible keyboard focus states, `prefers-reduced-motion` respected, real color contrast.

## Step 5 — Copy is part of the design, not filler

- Write from the end user's perspective, in words they'd actually use.
- Default to active voice. A button says exactly what it does ("Save changes," not "Submit").
- Errors state what happened and how to fix it, in the interface's voice.
- Cut selling language. Specificity beats cleverness.

## Step 6 — Screenshot Verification (CRITICAL)

Whenever you finish designing or building a UI component or page, you MUST automatically take a screenshot of it to visually review and verify your work. 
Do this every single time you update the UI to ensure it is correct.
- Use the script located at `tools/screenshot.js`.
- Command: `node tools/screenshot.js <URL_OR_FILE_PATH> <OUTPUT_IMAGE_PATH>`
- Example: `node tools/screenshot.js http://localhost:8080/ screenshots/review.png`
- If you are building a static HTML file, use the absolute file path: `node tools/screenshot.js file://C:/Users/chheu/Desktop/Cinema-microservice/Admin%20UI/index.html screenshots/review.png`
- After generating the screenshot, you must view the image to ensure the UI renders correctly, follows your design principles, and avoids AI slop. Make adjustments if necessary until the screenshot looks perfect.

## Self-check before calling it done

Run down this list literally, item by item:
1. Could this exact palette/type/layout combination have come out of a prompt for a completely different product?
2. Does anything on the Step 2 slop list appear here without the brief asking for it?
3. Is there more than one "bold" element competing for attention?
4. Does every animation respond to something the user did, except for the one deliberate exception?
5. Did you run the screenshot verification script (`node tools/screenshot.js ...`) and visually inspect the result?
6. Would a person in this product's actual audience recognize this as speaking their language?
