using Xunit;

namespace JohnKnoop.MongoRepository.IntegrationTests
{
    [CollectionDefinition("IntegrationTests", DisableParallelization = true)]
	public class TestsCollection : ICollectionFixture<LaunchSettingsFixture>
	{
		// This class has no code, and is never created. Its purpose is simply
		// to be the place to apply [CollectionDefinition] and all the
		// ICollectionFixture<> interfaces.
	}
}
