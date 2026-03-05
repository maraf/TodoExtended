### 2026-03-05T14:46:00Z: No code  refactor reusable Blazor componentsduplication 
**By:** Marek Fiera (user directive, captured by Squad)
**What:** When reviewing or designing, always identify duplicated UI patterns across pages and extract them into shared Blazor components. Never allow the same markup+logic to exist in two places. The `ToggleTaskStatus` pattern was the first violation  it existed in both Tasks.razor and Today.razor.found 
**Why:** User  enforce DRY principle for Blazor components as a standing team rule.directive 
