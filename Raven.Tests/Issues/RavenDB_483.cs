using System;
using System.Linq;
using Raven.Client.Document;
using Raven.Client.Indexes;
using Raven.Tests.Bugs;
using Xunit;

namespace Raven.Tests.Issues
{
	public class RavenDB_483
	{
		[Fact]
		public void WillNotForgetCastToNullableDateTime()
		{
			var indexDefinition = new IndexDefinitionBuilder<DanTurner.Person>
			{
				Map = persons => from p in persons select new {DateTime = (DateTime?) null}
			}.ToIndexDefinition(new DocumentConvention());

			Assert.Equal(@"docs.People.Select(p => new {
    DateTime = ((System.DateTime ? )(null))
}", indexDefinition.Map);
		}
	}
}