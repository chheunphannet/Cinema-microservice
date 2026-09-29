# Workspace Rules

## AWS Dashboard Clone UI Design

This project is an **Admin Dashboard** for a cinema microservice system. Its main purposes are:
- Configuration management
- Checking logs
- Managing data
- Generating reports

**Design Style:**
The UI must be based on the AWS Cloudscape Design System (an AWS Dashboard clone). It doesn't have to be a 100% pixel-perfect clone, but it must use the structural style, data-dense layouts, and the exact color palettes provided below.

### Color Palette (AWS Clone)

You MUST use these specific colors (you can convert RGB to Hex or use rgb() in your CSS/Tailwind config):

**Dark Mode Palette:**
- **Backgrounds:**
  - Base01: `rgb(35, 47, 60)`
  - Base02: `rgb(59, 76, 89)`
  - Base03: `rgb(78, 88, 98)`
  - Base04: `rgb(73, 86, 90)`
- **Foregrounds / Text:**
  - Base1: `rgb(250, 250, 250)` (Primary text)
  - Base2: `rgb(143, 168, 194)` (Secondary text)
- **Accents:**
  - Orange: `rgb(253, 152, 49)`
  - Red: `rgb(249, 90, 83)`
  - Pink: `rgb(252, 85, 139)`
  - Purple: `rgb(157, 113, 249)`
  - Blue: `rgb(78, 134, 249)`
  - Cyan: `rgb(34, 163, 196)`
  - Turquoise: `rgb(92, 191, 168)`
  - Green: `rgb(112, 171, 74)`

**Light Mode Palette:**
- **Backgrounds:**
  - Base01: `rgb(250, 250, 250)`
  - Base02: `rgb(234, 243, 233)`
  - Base03: `rgb(230, 242, 248)`
  - Base04: `rgb(239, 240, 243)`
- **Foregrounds / Text:**
  - Base1: `rgb(35, 48, 61)` (Primary text)
  - Base2: `rgb(90, 109, 132)` (Secondary text)
- **Accents:**
  - Orange: `rgb(214, 102, 42)`
  - Red: `rgb(211, 42, 53)`
  - Pink: `rgb(202, 43, 100)`
  - Purple: `rgb(101, 71, 192)`
  - Blue: `rgb(53, 81, 199)`
  - Cyan: `rgb(23, 128, 182)`
  - Turquoise: `rgb(36, 122, 104)`
  - Green: `rgb(68, 132, 48)`

**Implementation Rules:**
- Follow the `frontend-design` and `astro` skills, but OVERRIDE any dynamically generated color palette with the exact AWS palettes listed above.
- Ensure the interface supports both Light and Dark mode toggling.

**Branding (Logo):**
- Use the provided "net" logo for the dashboard header/navbar.
- The SVG version is located at `logo/net-logo.svg`.
- The logo is a clone of the Amazon logo, featuring dark blue text with an orange smile arrow. Use it prominently in the top-left of the layout.

**Design Foundation:**
- For detailed instructions on typography, spacing, content density, and layout rules, read `.agents/docs/Cloudscape_Foundation.md`.
