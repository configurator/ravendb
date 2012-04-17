using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Raven.Abstractions.Indexing;
using Raven.Client.Document;
using Raven.Client.Indexes;
using Raven.Client.Linq;
using Xunit;

namespace Raven.Tests.Bugs.ChananBraunstein
{
	public class ChananBraunstein : RavenTest
	{
		[Fact]
		public void RunTest()
		{
			using (GetNewServer())
			using (var store = new DocumentStore { Url = "http://localhost:8079/" }.Initialize())
			{
				store.ExecuteIndex(new RoundAssignedTo());

				using (var session = store.OpenSession())
				{
					session.Store(new RoutingEvent { DueDate = DateTime.Now });
					session.Store(new RoutingEvent { DueDate = DateTime.Now });
					session.Store(new RoutingEvent { DueDate = DateTime.Now });

					session.Store(
						new Project
						{
							Rounds = new List<Round> {
								new Round { AssignedTo = "me", AssignedBy = "you", Name = "round 1" },
								new Round { AssignedTo = "you", AssignedBy = "me", Name = "round 2" }
							},
							RoutingEventId = "RoutingEvents/1",
							IsActive = true
						});
					session.Store(
						new Project
						{
							Rounds = new List<Round> {
								new Round { AssignedTo = "me", AssignedBy = "me", Name = "round 1" },
								new Round { AssignedTo = "me", AssignedBy = "me", Name = "round 2" }
							},
							RoutingEventId = "RoutingEvents/2",
							IsActive = true
						});
					session.SaveChanges();
				}


				using (var session = store.OpenSession())
				{
					var results = session.Query<RoundAssignedTo.Result, RoundAssignedTo>()
						.AsProjection<RoundAssignedTo.Result>()
						.Customize(x => x.WaitForNonStaleResults()).ToList();

					Assert.NotEmpty(results);
				}
			}
		}

		public class Project
		{
			public IEnumerable<Round> Rounds { get; set; }

			public bool IsActive { get; set; }
			public string RoutingEventId { get; set; }
		}

		public class Round
		{
			public string AssignedTo { get; set; }
			public string AssignedBy { get; set; }
			public string Name { get; set; }
		}
		
		public class RoutingEvent
		{
			public string Id { get; set; }
			public DateTime DueDate { get; set; }
		}


		public class RoundAssignedTo : AbstractIndexCreationTask<Project, RoundAssignedTo.Result>
		{
			public class Result
			{
				public string FullName { get; set; }
				public string RoundName { get; set; }
				public string AssignedBy { get; set; }
				public string RoutingEventId { get; set; }
				public DateTime DueDate { get; set; }
			}

			public RoundAssignedTo()
			{
				Map = projects => from project in projects
								  let round = project.Rounds.Last()
								  where project.IsActive && round.AssignedTo != null
								  select new
								  {
									  FullName = round.AssignedTo,
									  RoundName = round.Name,
									  AssignedBy = round.AssignedBy,
									  RoutingEventId = project.RoutingEventId
								  };

				TransformResults = (database, results) => from result in results
														  let routingEvent =
															  database.Load<RoutingEvent>(result.RoutingEventId)
														  orderby routingEvent.DueDate
														  select new
														  {
															  result.FullName,
															  result.RoundName,
															  result.AssignedBy,
															  result.RoutingEventId,
															  routingEvent.DueDate
														  };

				Store(x => x.AssignedBy, FieldStorage.Yes);
				Store(x => x.RoundName, FieldStorage.Yes);
				Store(x => x.FullName, FieldStorage.Yes);
				Store(x => x.RoutingEventId, FieldStorage.Yes);
			}
		}

	}
}
