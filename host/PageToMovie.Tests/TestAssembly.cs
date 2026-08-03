using Xunit;

// SupportedModelCatalog is intentionally process-global because it mirrors the server's
// single catalog. Tests that reload alternate catalogs must not race tests reading it.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
