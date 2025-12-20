# CSS Architecture Documentation

## Overview

The QingFeng Portal application now uses a modular CSS architecture designed to facilitate future theme implementations and maintainability.

## File Structure

### Core CSS Files (in order of loading)

1. **theme-variables.css** - Theme Variables & Design Tokens
   - All CSS custom properties (variables)
   - Color palette
   - Spacing scale
   - Border radius values
   - Shadow definitions
   - Z-index layers
   - Transition timings
   
2. **layout.css** - Layout & Structural Styles
   - Page containers
   - Header/footer layouts
   - Background effects
   - Grid systems
   - Scrollbar customization
   
3. **components.css** - Reusable UI Components
   - Cards and panels
   - Buttons
   - Progress bars
   - Forms and inputs
   - Badges
   - Tables
   - Icons
   - Tooltips
   - Loading states
   
4. **pages.css** - Page-Specific Styles
   - Home page layouts
   - Clock & date displays
   - Weather widgets
   - Search bars
   - Status sections
   - Apps grids
   
5. **dock.css** - Navigation Dock
   - Bottom dock container
   - Dock items
   - Tooltips
   - Active indicators
   - Hover effects
   
6. **utilities.css** - Utility Classes
   - Bootstrap overrides
   - Text utilities
   - Color classes
   - Size classes
   - Modal/dialog styles
   - Settings & management page styles

## CSS Variables (Theme System)

All theme-related values are defined as CSS custom properties in `theme-variables.css`. This allows for easy theme switching in the future.

### Main Variable Categories

#### Colors
```css
--primary-color: #f9f506;
--secondary-color: #764ba2;
--success-color: #43e97b;
--danger-color: #f5576c;
--warning-color: #fbbf24;
--info-color: #4facfe;
```

#### Backgrounds
```css
--bg-light: #f8f8f5;
--bg-medium: #e8e8e8;
--bg-dark: #1a1f3a;
```

#### Surfaces (Cards, Panels)
```css
--surface-light: #ffffff;
--surface-medium: rgba(255, 255, 255, 0.5);
--surface-dark: rgba(30, 41, 59, 0.5);
```

#### Spacing
```css
--spacing-xs: 0.25rem;
--spacing-sm: 0.5rem;
--spacing-md: 0.75rem;
--spacing-lg: 1rem;
--spacing-xl: 1.5rem;
--spacing-2xl: 2rem;
--spacing-3xl: 2.5rem;
```

#### Border Radius
```css
--radius-sm: 0.5rem;
--radius-md: 0.75rem;
--radius-lg: 1rem;
--radius-xl: 1.25rem;
--radius-2xl: 2rem;
--radius-3xl: 3rem;
--radius-round: 9999px;
```

## Inline Styles Policy

### When to Use Inline Styles

Inline styles should ONLY be used for **dynamic values** that come from:
- Database/API (e.g., `style="background: @app.IconColor;"`)
- Runtime calculations (e.g., `style="width: @CpuUsage%"`)
- User preferences stored in state

### Examples of Acceptable Inline Styles
```razor
<!-- Dynamic color from database -->
<div class="app-icon" style="background: @app.IconColor;"></div>

<!-- Dynamic width from runtime calculation -->
<div class="progress-bar" style="width: @percentage%"></div>
```

### What Should Be CSS Classes

All static styling should use CSS classes:
```razor
<!-- ❌ Bad: Static inline style -->
<i class="icon" style="font-size: 24px; color: blue;"></i>

<!-- ✅ Good: Use CSS class -->
<i class="icon icon-md color-primary"></i>
```

## Adding a New Theme

To add a new theme in the future:

1. **Option A: CSS Variables Override**
   - Create a new CSS file (e.g., `theme-dark.css`)
   - Override the CSS variables:
   ```css
   :root {
       --primary-color: #new-color;
       --bg-light: #new-bg;
       /* ... override other variables */
   }
   ```
   - Conditionally load this file based on user preference

2. **Option B: Media Query**
   - Already prepared in `theme-variables.css`:
   ```css
   @media (prefers-color-scheme: dark) {
       :root {
           /* Override variables for dark mode */
       }
   }
   ```

3. **Option C: CSS Class Toggle**
   - Add theme classes to root element:
   ```html
   <body class="theme-dark">
   ```
   - Define theme-specific variables:
   ```css
   .theme-dark {
       --primary-color: #different-color;
       /* ... */
   }
   ```

## Best Practices

1. **Always use CSS variables** for colors, spacing, and other theme-related values
2. **Keep inline styles minimal** - only for truly dynamic values
3. **Use utility classes** for common patterns (spacing, colors, sizes)
4. **Component-scoped styles** should go in component-specific CSS files (e.g., `MainLayout.razor.css`)
5. **Page-specific styles** go in `pages.css`
6. **Reusable component styles** go in `components.css`

## Migration Notes

### What Was Changed

- **Removed Files:**
  - `app.css` - Merged into multiple new files
  - `bootstrap-custom.css` - Merged into utilities.css
  - `modern-home.css` - Merged into pages.css and layout.css
  - `home-portal.css` - Merged into pages.css and layout.css

- **Created Files:**
  - `theme-variables.css` - All CSS variables
  - `layout.css` - Layout structures
  - `components.css` - Reusable components
  - `pages.css` - Page-specific styles
  - `dock.css` - Navigation dock
  - `utilities.css` - Utility classes

- **Inline Style Cleanup:**
  - Removed static font-size, color, and background inline styles
  - Replaced with utility classes (e.g., `icon-sm`, `icon-md`, `color-primary`)
  - Kept dynamic inline styles (values from `@` Razor expressions)

### Benefits

1. **Better Organization** - Clear separation of concerns
2. **Easier Theming** - All theme values in CSS variables
3. **Better Maintainability** - Smaller, focused files
4. **Reduced Redundancy** - Eliminated duplicate styles
5. **Improved Performance** - Reduced total CSS size
6. **Future-Ready** - Easy to add dark mode or custom themes

## Common Utility Classes

### Text Colors
- `color-primary` - Primary theme color
- `color-secondary` - Secondary text color
- `color-muted` - Muted/light text
- `color-white` - White text
- `color-primary-blue` - Blue accent color

### Icon Sizes
- `icon-sm` - 20px
- `icon-md` - 24px
- `icon-lg` - 28px
- `icon-xl` - 32px
- `icon-2xl` - 4rem

### Width Utilities
- `w-150` - 150px width
- `w-200` - 200px width
- `w-250` - 250px width

### Height Utilities
- `h-20` - 20px height
- `h-24` - 24px height

### Z-index
- `z-1050` - Toast/notification level

## Responsive Design

All responsive breakpoints use Bootstrap's standard breakpoints:
- `xs`: < 576px
- `sm`: ≥ 576px
- `md`: ≥ 768px
- `lg`: ≥ 1024px
- `xl`: ≥ 1200px

Responsive utilities are defined in each CSS file where needed.
