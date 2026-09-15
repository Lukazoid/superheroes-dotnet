using Xunit;

// BattleController holds its comparison state in two static fields, so tests that exercise
// the /battle endpoint mutate process-global state. Disabling parallelization keeps that
// state's mutations deterministic between test classes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
