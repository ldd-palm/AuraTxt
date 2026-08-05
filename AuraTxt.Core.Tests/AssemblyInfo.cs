using Xunit;

// ConfigService.DefaultSettings is a shared static set by every ConfigService.Load() call
// across the suite (ConfigServiceTests, AiClientTests' TargetLanguage test, ...). xunit runs
// different test classes in parallel by default, so without this, one test's Load() can
// clobber DefaultSettings mid-assertion in another. The suite is small/fast, so serializing
// it costs nothing measurable.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
