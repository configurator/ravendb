using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NLog;
using Raven.Abstractions.Data;
using Raven.Database;
using Raven.Database.Storage;
using Raven.Imports.Newtonsoft.Json.Linq;
using Raven.Json.Linq;

namespace Raven.Bundles.Replication.Responders
{
	public abstract class SingleItemReplicationBehavior<TInternal, TExternal>
	{
		private readonly Logger log = LogManager.GetCurrentClassLogger();

		public DocumentDatabase Database { get; set; }
		public IStorageActionsAccessor Actions { get; set; }
		public string Src { get; set; }

		public void Replicate(string id, RavenJObject metadata, TExternal incoming)
		{
			TInternal existingItem;
			Guid existingEtag;
			var existingMetadata = TryGetExisting(id, out existingItem, out existingEtag);
			if (existingMetadata == null)
			{
				log.Debug("New item {0} replicated successfully from {1}", id, Src);
				AddWithoutConflict(id, metadata, incoming);
				return;
			}


			// we just got the same version from the same source - request playback again?
			// at any rate, not an error, moving on
			if (existingMetadata.Value<string>(Constants.RavenReplicationSource) == metadata.Value<string>(Constants.RavenReplicationSource)
			    && existingMetadata.Value<int>(Constants.RavenReplicationVersion) == metadata.Value<int>(Constants.RavenReplicationVersion))
			{
				return;
			}


			var existingDocumentIsInConflict = existingMetadata[Constants.RavenReplicationConflict] != null;
			if (existingDocumentIsInConflict == false &&                    // if the current document is not in conflict, we can continue without having to keep conflict semantics
			    (IsDirectChildOfCurrent(metadata, existingMetadata))) // this update is direct child of the existing doc, so we are fine with overwriting this
			{
				log.Debug("Existing item {0} replicated successfully from {1}", id, Src);
				AddWithoutConflict(id, metadata, incoming);
				return;
			}

			if (TryResolveConflict(id, metadata, incoming, existingItem))
			{
				AddWithoutConflict(id, metadata, incoming);
				return;
			}

			Database.TransactionalStorage.ExecuteImmediatelyOrRegisterForSyncronization(() =>
			                                                                            Database.RaiseNotifications(new DocumentChangeNotification
			                                                                            {
			                                                                            	Name = id,
			                                                                            	Type = DocumentChangeTypes.ReplicationConflict
			                                                                            }));

			metadata[Constants.RavenReplicationConflictDocument] = true;
			var newDocumentConflictId = id + "/conflicts/" + HashReplicationIdentifier(metadata);
			metadata.Add(Constants.RavenReplicationConflict, RavenJToken.FromObject(true));
			AddWithoutConflict(id, metadata, incoming);

			if (existingDocumentIsInConflict) // the existing document is in conflict
			{
				log.Debug("Conflicted item {0} has a new version from {1}, adding to conflicted documents", id, Src);

				AppendToCurrentItemConflicts(id, newDocumentConflictId, existingMetadata, existingItem);
				return;
			}
			log.Debug("Existing item {0} is in conflict with replicated version from {1}, marking item as conflicted", id, Src);

			// we have a new conflict
			// move the existing doc to a conflict and create a conflict document
			var existingDocumentConflictId = id + "/conflicts/" + HashReplicationIdentifier(existingEtag);

			CreateConflict(id, newDocumentConflictId, existingDocumentConflictId, existingItem, existingMetadata);
		}

		protected abstract void AddWithoutConflict(string id, RavenJObject metadata, TExternal incoming);

		protected abstract void CreateConflict(string id, string newDocumentConflictId, string existingDocumentConflictId, TInternal existingItem, RavenJObject existingMetadata);

		protected abstract void AppendToCurrentItemConflicts(string id, string newConflictId, RavenJObject existingMetadata, TInternal existingItem);

		protected abstract RavenJObject TryGetExisting(string id, out TInternal existingItem, out Guid existingEtag);

		protected abstract bool TryResolveConflict(string id, RavenJObject metadata, TExternal document,
		                                          TInternal existing);


		private static string HashReplicationIdentifier(RavenJObject metadata)
		{
			using (var md5 = MD5.Create())
			{
				var bytes = Encoding.UTF8.GetBytes(metadata.Value<string>(Constants.RavenReplicationSource) + "/" + metadata.Value<string>("@etag"));
				return new Guid(md5.ComputeHash(bytes)).ToString();
			}
		}

		private string HashReplicationIdentifier(Guid existingEtag)
		{
			using (var md5 = MD5.Create())
			{
				var bytes = Encoding.UTF8.GetBytes(Database.TransactionalStorage.Id + "/" + existingEtag);
				return new Guid(md5.ComputeHash(bytes)).ToString();
			}
		}


		private static bool IsDirectChildOfCurrent(RavenJObject incomingMetadata, RavenJObject existingMetadata)
		{
			var version = new RavenJObject
			{
				{Constants.RavenReplicationSource, existingMetadata[Constants.RavenReplicationSource]},
				{Constants.RavenReplicationVersion, existingMetadata[Constants.RavenReplicationVersion]},
			};

			var history = incomingMetadata[Constants.RavenReplicationHistory];
			if (history == null || history.Type == JTokenType.Null) // no history, not a parent
				return false;

			if (history.Type != JTokenType.Array)
				return false;

			return history.Values().Contains(version, new RavenJTokenEqualityComparer());
		}
	}
}