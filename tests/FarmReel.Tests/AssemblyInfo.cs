// The SQLite data layer uses a static database path - run tests sequentially.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
