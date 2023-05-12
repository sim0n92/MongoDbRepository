using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JohnKnoop.MongoRepository.IntegrationTests.ArrayFiltersTests;
using JohnKnoop.MongoRepository.IntegrationTests.TestEntities;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static JohnKnoop.MongoRepository.IntegrationTests.UpdateOneBulkTests;

namespace JohnKnoop.MongoRepository.IntegrationTests
{
	public class LaunchSettingsFixture : IDisposable
	{
		public LaunchSettingsFixture()
		{
			using (var file = File.OpenText("Properties\\launchSettings.json"))
			{
				var reader = new JsonTextReader(file);
				var jObject = JObject.Load(reader);

				var variables = jObject
					.GetValue("profiles");
				List<JProperty> src;

				if (variables != default) {
					//select a proper profile here
					src = variables.SelectMany(profiles => profiles.Children())
					.SelectMany(profile => profile.Children<JProperty>())
					.Where(prop => prop.Name == "environmentVariables")
					.SelectMany(prop => prop.Value.Children<JProperty>())
					.ToList();
				} else
                {
					src = new List<JProperty>();
                }

				foreach (var variable in src)
				{
					Environment.SetEnvironmentVariable(variable.Name, variable.Value.ToString());
				}
			}
		}

		public void MapDb()
		{
			//MongoRepository.Configure()
			//.Database("TestDb", x => x
			//		//.MapAlongWithSubclassesInSameAssebmly<MyBaseEntity>("MyEntities")
			//		.MapAlongWithSubclassesInSameAssebmly<DummyEntity>("DummyEntities")
			//		.Map<ArrayContainer>("ArrayContainers")
			//)
			//.AutoEnlistWithTransactionScopes()
			//.Build();

			MongoRepository.Configure()
	.DatabasePerTenant("TestDb", x => x
		.Map<Show>("MyShows")
		.MapAlongWithSubclassesInSameAssebmly<MyBaseEntity>("MyBaseEntities")
		.MapAlongWithSubclassesInSameAssebmly<DummyEntity>("DummyEntities")
		.MapAlongWithSubclassesInSameAssebmly<MySimpleEntity>("MySimpleEntities")
		.MapAlongWithSubclassesInSameAssebmly<Person>("MyPersons")
		.Map<ArrayContainer>("ArrayContainers")
		.MapAlongWithSubclassesInSameAssebmly<FileGroup>("FileGroups", x => x.WithIndex("Files.Name", unique: false))
		.Map<CustomMapped>(
			x => x
				.WithCustomClassMapping(
					cm => {
						cm.AutoMap();
						cm.MapIdProperty("Id").SetIdGenerator(ObjectIdGenerator.Instance);
						cm.MapProperty("Name").SetElementName("_name");
					}
			)
		)
		.Map<MyStandaloneEntity>("MyStandaloneEntities")
		.Map<Item>("Items")
	)
	.AutoEnlistWithTransactionScopes()
	.Build();

		}

		public void Dispose()
		{

		}
	}
}
