using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace JohnKnoop.MongoRepository.IntegrationTests
{
	public enum StorableKindEnum
    {
		A = 0,
		B = 1
    }

	public interface IStorable
	{
		string ContentType { get; set; }
		DateTime Created { get; set; }
		StorableKindEnum Kind { get; set; }
		string Name { get; set; }
		string SourceName { get; set; }
		string StorageId { get; set; }
		HashSet<string> Tags { get; set; }
	}

	public interface IStorableGroup
	{
		ObjectId Id { get; set; }
		/// <summary>
		/// List of arbitrary tags
		/// </summary>
		HashSet<string> Tags { get; set; }

		/// <summary>
		/// List of files of this snap
		/// </summary>
		List<MyFile> Files { get; set; }
	}

	[Serializable, JsonObject]
	[BsonDiscriminator(Required = true)]
	[BsonKnownTypes(typeof(MyFile))]
	public class MyFile : IStorable
	{
		

        /// <summary>
        /// Foto name / file name
        /// </summary>
        public string Name { get; set; }

		/// <summary>
		/// File mime type
		/// </summary>
		public string ContentType { get; set; }

		/// <summary>
		/// Storage Id, can be any kind of path, absolute relative, file will be loacated using this path
		/// </summary>
		public string StorageId { get; set; }

		/// <summary>
		/// DateTime of foto creation
		/// </summary>
		public DateTime Created { get; set; }

		/// <summary>
		/// Name of camera what did this foto
		/// </summary>
		public string SourceName { get; set; }

		/// <summary>
		/// Kind of foto
		/// </summary>
		public StorableKindEnum Kind { get; set; }

		/// <summary>
		/// List of arbitrary tags
		/// </summary>
		public HashSet<string> Tags { get; set; } = new HashSet<string>();



	}

	[Serializable, JsonObject]
	[BsonDiscriminator(Required = true)]
	[BsonKnownTypes(typeof(FileGroup))]
	public class FileGroup : IStorableGroup
	{
		public FileGroup()
        {
			Id = new ObjectId();
        }

		public ObjectId Id { get; set; }

		private HashSet<string> _tags;
		private List<MyFile> _files;

		/// <summary>
		/// List of arbitrary tags
		/// </summary>
		[BsonIgnoreIfNull]
		public HashSet<string> Tags
		{
			get { return _tags ?? (_tags = new HashSet<string>()); }
			set { _tags = value; }
		}

		/// <summary>
		/// List of fotos of this snap
		/// </summary>
		[BsonIgnoreIfNull]
		public List<MyFile> Files
		{
			get { return _files ?? (_files = new List<MyFile>()); }
			set { _files = value; }
		}
	}

	[Serializable, JsonObject]
	[BsonDiscriminator(Required = true)]
	[BsonKnownTypes(typeof(CustomMapped))]
	public class CustomMapped
    {
		public CustomMapped(string name)
		{
			Name = name;
		}

		public void SetOther(string val)
        {
			OtherProtected = val;
		}

		public string Id { get; set; }

		public string Name { get; protected set; }

		public string OtherProtected { get; protected set; }
	}

	[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
    public class InsertAndFindOneSpeedTests : IClassFixture<LaunchSettingsFixture>
    {
		private const string DbName = "TestDb";
		private const string CollectionName = "FileGroups";
		private readonly MongoClient _mongoClient;
		private readonly IRepository<FileGroup> _repository;
		private readonly string _baseEntityId;
		private readonly string _derivedEntityId;

		private readonly ITestOutputHelper _testOutputHelper;

		public InsertAndFindOneSpeedTests(LaunchSettingsFixture launchSettingsFixture, ITestOutputHelper testOutputHelper)
		{
			_testOutputHelper = testOutputHelper;
			_mongoClient = new MongoClient(Environment.GetEnvironmentVariable("MongoDbConnectionString"));

			//var db = _mongoClient.GetDatabase("admin");
			//var command = new BsonDocumentCommand<BsonDocument>(
			//		new BsonDocument() { { "replSetGetStatus", 1 } });

			//try
			//{
			//	var res = db.RunCommand<BsonDocument>(command);
			//	bool rsOk = false;
			//	if (res.TryGetValue("set", out BsonValue name))
			//	{
			//		if (name.AsString == "rs0")
			//		{
			//			if (res.TryGetValue("myState", out BsonValue state))
			//			{
			//				if (state.AsInt32 == 1)
			//				{
			//					rsOk = true;
			//				}
			//			}
			//		}
			//	}
			//}catch(Exception ex)
   //         {
			//	int i = 0;
   //         }

			//_mongoClient = new MongoClient(Environment.GetEnvironmentVariable("MongoDbConnectionString"));

			MongoRepository.Configure()
				.Database(DbName, x => x
					.MapAlongWithSubclassesInSameAssebmly<FileGroup>(CollectionName
                    ,
                        x => x
                            .WithIndex("Files.Name", unique: false)
                    )
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
                )
				.AutoEnlistWithTransactionScopes()
				.Build();

			// Empty all collections in database
			foreach (var collectionName in _mongoClient.GetDatabase(DbName).ListCollectionNames().ToEnumerable())
			{
				if (!collectionName.Contains("system"))
				{
					_mongoClient.GetDatabase(DbName).GetCollection<BsonDocument>(collectionName).DeleteMany(x => true);
				}
			}


			_repository = _mongoClient.GetRepository<FileGroup>();

		}

		private async Task AssertNumberOfDocumentsInCollection(int expected)
		{
			var documentsInCollection = await _mongoClient.GetDatabase(DbName).GetCollection<MyBaseEntity>(CollectionName).CountDocumentsAsync(x => true);
			documentsInCollection.Should().Be(expected);
		}

		private async Task AssertNumberOfDocumentsInTrash(int expected)
		{
			var docsInTrash = await _mongoClient.GetDatabase(DbName).GetCollection<BsonDocument>("DeletedObjects").CountDocumentsAsync(x => true);
			docsInTrash.Should().Be(expected);
		}

		[Fact]
		public async Task Custom_Mappings_test()
        {
			var r = _mongoClient.GetRepository<CustomMapped>();
			CustomMapped cm = new CustomMapped("pippo");
			cm.SetOther("Other value");
			await r.InsertAsync(cm);

			var rr = await r.GetAsync(cm.Id.ToString());

			Assert.True(rr.Name == cm.Name);
			Assert.True(rr.OtherProtected == cm.OtherProtected);
		}


		private static string[] Tags = new string[] {"A", "B", "C", "D", "E", "F", "G", "H" };

		private static Random rand = new Random((int)DateTime.Now.Ticks);

		private HashSet<string> GetTags()
        {
			int len = rand.Next(Tags.Length) + 1;
			return Tags.Take(len).ToHashSet();
        }

		private MyFile MakeFile(int id)
        {
			MyFile f = new MyFile();
			f.Name = $"File_{id}";
			f.Tags = GetTags();
			f.Created = DateTime.Now;

			return f;
        }

		private int MaxId = 0;

		private FileGroup MakeRecord()
        {
			FileGroup ret = new FileGroup();
			ret.Tags = GetTags();

			for(int i = 0; i < rand.Next(5) + 1; ++i)
            {
				ret.Files.Add(MakeFile(MaxId ++));
            }

			return ret;
        }

		private async Task Insert(FileGroup f)
        {
			await _repository.WithTransactionAsync(async () =>
			{
				await _repository.InsertAsync(f);
			}, TransactionType.TransactionScope, maxRetries: 3);
		}

		[Fact]
		public async Task Insert_10000_records()
		{
			List<FileGroup> records = new List<FileGroup>();

			int cntStarts1 = 0;

			for (int i = 0; i < 10000; ++i)
			{
				var r = MakeRecord();
				records.Add(r);
				if(r.Files.Any(f => f.Name.StartsWith("File_1")))
                {
					++cntStarts1;
                }
			}

			Stopwatch sw = new Stopwatch();

			sw.Start();
			records.ForEach(r => {
				Insert(r).Wait();
				// _repository.InsertAsync(r).Wait();
			});
				
			
			sw.Stop();

			_testOutputHelper.WriteLine($"Time: {sw.ElapsedMilliseconds}/{(double)sw.ElapsedMilliseconds/10000}");

			HashSet<string> what = new HashSet<string>() { "A", "B", "C", "D", "E", "F", "G", "H" };

			sw.Restart();
			// search
			var ret = _repository.Query().Where(x => what.All(e => x.Tags.Contains(e))).ToList();

			sw.Stop();

			_testOutputHelper.WriteLine($"Search Time: {sw.ElapsedMilliseconds}");

			foreach (var el in ret)
            {
				Assert.True(what.IsSubsetOf(el.Tags));
            }

			// get all groups where at least 1 file have tags match
			sw.Restart();
			//var ret1 = _repository.Query().Where(x => x.Files.Any(f => what.All(e => f.Tags.Contains(e)))).ToList();
			// var ret1 = _repository.Query().Where(x => x.Files.Any(f => f.Name.StartsWith("File_1"))).ToList();
			var ret1 = _repository.Query().Where(x => x.Files.Any(f => f.Name == "File_9780")).ToList();

			sw.Stop();

			_testOutputHelper.WriteLine($"Search I Time: {sw.ElapsedMilliseconds}");

			// Assert.True(ret1.Count == cntStarts1);

			Assert.True(ret1.Count == 1);


			//foreach (var el in ret1)
			//{
			//    Assert.True(el.Files.Any(f => what.IsSubsetOf(f.Tags)));
			//}


			// By id
			//var doc = await _repository.FindOneAndDeleteAsync(r.I);
			//doc.Name.Should().Be("Mary");
			await AssertNumberOfDocumentsInCollection(10000);
		}

		
    }
}
