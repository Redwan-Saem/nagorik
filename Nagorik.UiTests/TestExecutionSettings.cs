using Xunit;

// These tests each launch Chrome and an isolated web server. Running collections
// serially avoids competing browser processes starving local login requests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
