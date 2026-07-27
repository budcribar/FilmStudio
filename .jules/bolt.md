## 2024-05-18 - Caching Tokens on BookContextService
**Learning:** `Tokenize` was repeatedly parsing regex patterns `[a-z]{3,}` for every chunk and text body processed during fuzzy matching of scene body texts, creating unnecessary string LINQ allocations.
**Action:** Extract compiled `Regex` static properties and memoize token creation on the `BookPage` model so string tokenizations only happen once per page, bringing complexity from O(scenes * pages) down to O(pages).
