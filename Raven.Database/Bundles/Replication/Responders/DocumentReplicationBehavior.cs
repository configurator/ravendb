using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Abstractions.Data;
using Raven.Bundles.Replication.Plugins;
using Raven.Json.Linq;

namespace Raven.Bundles.Replication.Responders
{
	public class DocumentReplicationBehavior : SingleItemReplicationBehavior<JsonDocument, RavenJObject>
	{
		public IEnumerable<AbstractDocumentReplicationConflictResolver> ReplicationConflictResolvers { get; set; }

		protected override void AddWithoutConflict(string id, RavenJObject metadata, RavenJObject incoming)
		{
			Actions.Documents.AddDocument(id, Guid.Empty, incoming, metadata);
		}

		protected override void CreateConflict(string id, string newDocumentConflictId, 
			string existingDocumentConflictId, JsonDocument existingItem, RavenJObject existingMetadata)
		{
			existingMetadata.Add(Constants.RavenReplicationConflict, true);
			Actions.Documents.AddDocument(existingDocumentConflictId, null, existingItem.DataAsJson, existingItem.Metadata);
			Actions.Documents.AddDocument(id, null,
			                              new RavenJObject
			                              {
			                              	{"Conflicts", new RavenJArray(existingDocumentConflictId, newDocumentConflictId)}
			                              },
			                              new RavenJObject
			                              {
			                              	{Constants.RavenReplicationConflict, true},
			                              	{"@Http-Status-Code", 409},
			                              	{"@Http-Status-Description", "Conflict"}
			                              });
		}

		protected override void AppendToCurrentItemConflicts(string id, string newConflictId, RavenJObject existingMetadata, JsonDocument existingItem)
		{
			// just update the current doc with the new conflict document
			existingItem.DataAsJson.Value<RavenJArray>("Conflicts").Add(RavenJToken.FromObject(newConflictId));
			Actions.Documents.AddDocument(id, existingItem.Etag, existingItem.DataAsJson, existingItem.Metadata);
		}

		protected override RavenJObject TryGetExisting(string id, out JsonDocument existingItem, out Guid existingEtag)
		{
			var existingDoc = Actions.Documents.DocumentByKey(id, null);
			if(existingDoc != null)
			{
				existingItem = existingDoc;
				existingEtag = existingDoc.Etag.Value;
				return existingDoc.Metadata;
			}

			var listItem = Actions.Lists.Read(Constants.RavenReplicationDocsTombstones, id);
			if(listItem != null)
			{
				existingEtag = listItem.Etag;
				existingItem = new JsonDocument
				{
					Etag = listItem.Etag,
					DataAsJson = new RavenJObject(),
					Key = listItem.Key,
					Metadata = listItem.Data
				};
				return listItem.Data;
			}
			existingEtag = Guid.Empty;
			existingItem = null;
			return null;

		}

		protected override bool TryResolveConflict(string id, RavenJObject metadata, RavenJObject document, JsonDocument existing)
		{
			return ReplicationConflictResolvers.Any(
					replicationConflictResolver => replicationConflictResolver.TryResolve(id, metadata, document, existing));
		}
	}
}