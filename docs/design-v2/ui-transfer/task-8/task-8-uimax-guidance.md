## UI Pro Max Search Results
**Domain:** ux | **Query:** consistent controls progressive disclosure quantity stepper inline editing discoverability
**Source:** ux-guidelines.csv | **Found:** 3 results

### Result 1
- **Category:** Forms
- **Issue:** Inline Validation
- **Platform:** All
- **Description:** Validate as user types or on blur
- **Do:** Validate on blur for most fields
- **Don't:** Validate only on submit
- **Code Example Good:** onBlur validation
- **Code Example Bad:** Submit-only validation
- **Severity:** Medium

### Result 2
- **Category:** Data Entry
- **Issue:** Bulk Actions
- **Platform:** Web
- **Description:** Editing one by one is tedious
- **Do:** Allow multi-select and bulk edit
- **Don't:** Single row actions only
- **Code Example Good:** Checkbox column + Action bar
- **Code Example Bad:** Repeated actions per row
- **Severity:** Low

### Result 3
- **Category:** Typography
- **Issue:** Font Size Scale
- **Platform:** All
- **Description:** Consistent type hierarchy aids scanning
- **Do:** Use consistent modular scale
- **Don't:** Random font sizes
- **Code Example Good:** Type scale (12 14 16 18 24 32)
- **Code Example Bad:** Arbitrary sizes
- **Severity:** Medium



## UI Pro Max Stack Guidelines
**Stack:** wpf | **Query:** buttons input focus keyboard validation layout
**Source:** stacks/wpf.csv | **Found:** 6 results

### Result 1
- **Category:** Data Binding
- **Guideline:** Use INotifyDataErrorInfo for validation
- **Description:** Surface validation errors to the binding system instead of ad-hoc error UI
- **Do:** ObservableValidator with DataAnnotations attributes
- **Don't:** Throwing in setters or maintaining separate error properties
- **Code Good:** public partial class FormVm : ObservableValidator { [ObservableProperty][NotifyDataErrorInfo][Required] private string _email; }
- **Code Bad:** if (string.IsNullOrEmpty(Email)) ErrorMessage = "Required";
- **Severity:** Medium
- **Docs URL:** https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observablevalidator

### Result 2
- **Category:** Accessibility
- **Guideline:** Support keyboard navigation
- **Description:** All functionality reachable via keyboard
- **Do:** Tab order and KeyboardNavigation properties
- **Don't:** Mouse-only interactions
- **Code Good:** <StackPanel KeyboardNavigation.TabNavigation="Cycle">
- **Code Bad:** Click handlers with no keyboard equivalent
- **Severity:** High
- **Docs URL:** https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/focus-overview

### Result 3
- **Category:** Commands
- **Guideline:** Use InputBindings for keyboard shortcuts
- **Description:** Bind keyboard gestures to commands without manual key handling
- **Do:** KeyBinding inside Window.InputBindings
- **Don't:** Custom key handling in PreviewKeyDown
- **Code Good:** <Window.InputBindings><KeyBinding Key="S" Modifiers="Ctrl" Command="{Binding SaveCommand}"/></Window.InputBindings>
- **Code Bad:** PreviewKeyDown handler checking for Ctrl+S
- **Severity:** Medium
- **Docs URL:** https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview

### Result 4
- **Category:** Commands
- **Guideline:** Use CanExecute for enable/disable
- **Description:** Automatically disable controls when action unavailable; call NotifyCanExecuteChanged when state changes
- **Do:** CanExecute returning false to disable buttons
- **Don't:** IsEnabled binding to a separate bool
- **Code Good:** new RelayCommand(Save, () => !IsBusy)
- **Code Bad:** <Button IsEnabled="{Binding IsNotBusy}"/>
- **Severity:** Medium
- **Docs URL:** https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview

### Result 5
- **Category:** Architecture
- **Guideline:** Keep code-behind minimal
- **Description:** Code-behind should only contain view-specific logic
- **Do:** View logic like focus management and animations in code-behind
- **Don't:** Business logic and data access in code-behind
- **Code Good:** Loaded handler that sets initial focus
- **Code Bad:** Loaded handler that calls database and populates grid
- **Severity:** Medium
- **Docs URL:** https://learn.microsoft.com/en-us/dotnet/desktop/wpf/xaml/

### Result 6
- **Category:** Layout
- **Guideline:** Avoid hardcoded sizes
- **Description:** Use Auto Star and MinWidth/MaxWidth
- **Do:** Proportional sizing with * and Auto
- **Don't:** Fixed pixel widths on resizable content
- **Code Good:** <ColumnDefinition Width="2*"/>
- **Code Bad:** <ColumnDefinition Width="350"/> on main content
- **Severity:** Medium
- **Docs URL:** https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/grid



## UI Pro Max Search Results
**Domain:** ux | **Query:** animation accessibility z-index loading
**Source:** ux-guidelines.csv | **Found:** 6 results

### Result 1
- **Category:** Animation
- **Issue:** Loading States
- **Platform:** All
- **Description:** Show feedback during async operations
- **Do:** Use skeleton screens or spinners
- **Don't:** Leave UI frozen with no feedback
- **Code Example Good:** animate-pulse skeleton
- **Code Example Bad:** Blank screen while loading
- **Severity:** High

### Result 2
- **Category:** Animation
- **Issue:** Continuous Animation
- **Platform:** All
- **Description:** Infinite animations are distracting
- **Do:** Use for loading indicators only
- **Don't:** Use for decorative elements
- **Code Example Good:** animate-spin on loader
- **Code Example Bad:** animate-bounce on icons
- **Severity:** Medium

### Result 3
- **Category:** Layout
- **Issue:** Stacking Context
- **Platform:** Web
- **Description:** New stacking contexts reset z-index
- **Do:** Understand what creates new stacking context
- **Don't:** Expect z-index to work across contexts
- **Code Example Good:** Parent with z-index isolates children
- **Code Example Bad:** z-index: 9999 not working
- **Severity:** Medium

### Result 4
- **Category:** Layout
- **Issue:** Z-Index Management
- **Platform:** Web
- **Description:** Stacking context conflicts cause hidden elements
- **Do:** Define z-index scale system (10 20 30 50)
- **Don't:** Use arbitrary large z-index values
- **Code Example Good:** z-10 z-20 z-50
- **Code Example Bad:** z-[9999]
- **Severity:** High

### Result 5
- **Category:** Performance
- **Issue:** Lazy Loading
- **Platform:** All
- **Description:** Load content as needed
- **Do:** Lazy load below-fold images and content
- **Don't:** Load everything upfront
- **Code Example Good:** loading='lazy'
- **Code Example Bad:** All images eager load
- **Severity:** Medium

### Result 6
- **Category:** Performance
- **Issue:** Font Loading
- **Platform:** Web
- **Description:** Web fonts can block rendering
- **Do:** Use font-display swap or optional
- **Don't:** Invisible text during font load
- **Code Example Good:** font-display: swap
- **Code Example Bad:** FOIT (Flash of Invisible Text)
- **Severity:** Medium
