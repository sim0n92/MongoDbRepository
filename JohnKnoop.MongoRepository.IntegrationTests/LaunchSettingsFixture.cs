using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

		public void Dispose()
		{

		}
	}
}