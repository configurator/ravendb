using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Raven.Imports.Newtonsoft.Json;
using Raven.Abstractions.Data;
using Raven.Abstractions.Indexing;
using Raven.Studio.Commands;
using Raven.Studio.Features.Input;
using Raven.Studio.Infrastructure;
using Raven.Studio.Messages;

namespace Raven.Studio.Models
{
	public class IndexDefinitionModel : PageViewModel, IHasPageTitle
	{
		private readonly Observable<DatabaseStatistics> statistics;
		private IndexDefinition index;
		private string originalIndex;
	    private bool isNewIndex;
	    private bool hasUnsavedChanges;

		public IndexDefinitionModel()
		{
			ModelUrl = "/indexes/";
			index = new IndexDefinition();
			Maps = new BindableCollection<MapItem>(x => x.Text)
			{
				new MapItem()
			};

		    Maps.CollectionChanged += HandleChildCollectionChanged;

			Fields = new BindableCollection<FieldProperties>(field => field.Name);
		    Fields.CollectionChanged += HandleChildCollectionChanged;

			statistics = Database.Value.Statistics;
			statistics.PropertyChanged += (sender, args) => OnPropertyChanged(() => ErrorsCount);
		}

	    private void HandleChildCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	    {
	        MarkAsDirty();

            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                var newItem = e.NewItems[0] as INotifyPropertyChanged;
                if (newItem != null)
                {
                    newItem.PropertyChanged += HandleChildItemChanged;
                }
            }
	    }

	    private void HandleChildItemChanged(object sender, PropertyChangedEventArgs e)
	    {
	        MarkAsDirty();
	    }

	    private void UpdateFromIndex(IndexDefinition indexDefinition)
		{
			index = indexDefinition;

			if (index.Maps.Count == 0)
			{
				index.Maps.Add("");
			}

			Maps.Set(index.Maps.Select(x => new MapItem {Text = x}));

			ShowReduce = Reduce != null;
			ShowTransformResults = TransformResults != null;

			CreateOrEditField(index.Indexes, (f, i) => f.Indexing = i);
			CreateOrEditField(index.Stores, (f, i) => f.Storage = i);
			CreateOrEditField(index.SortOptions, (f, i) => f.Sort = i);
			CreateOrEditField(index.Analyzers, (f, i) => f.Analyzer = i);

	        hasUnsavedChanges = false;

			OnEverythingChanged();
		}

		public override void LoadModelParameters(string parameters)
		{
			var urlParser = new UrlParser(parameters);
			if (urlParser.GetQueryParam("mode") == "new")
			{
				IsNewIndex = true;
				Header = "New Index";

				UpdateFromIndex(new IndexDefinition());

				return;
			}

			var name = urlParser.Path;
			if (string.IsNullOrWhiteSpace(name))
				HandleIndexNotFound(null);

			Header = name;
			IsNewIndex = false;

		    DatabaseCommands.GetIndexAsync(name)
		        .ContinueOnUIThread(task =>
		                                {
		                                    if (task.IsFaulted || task.Result == null)
		                                    {
		                                        HandleIndexNotFound(name);
		                                        return;
		                                    }
		                                    originalIndex = JsonConvert.SerializeObject(task.Result);
		                                    UpdateFromIndex(task.Result);
		                                }).Catch();
		}

        public override bool CanLeavePage()
        {
            if (hasUnsavedChanges)
            {
                return AskUser.Confirmation("Edit Index",
                                            "There are unsaved changes to this index. Are you sure you want to continue?");
            }
            else
            {
                return true;
            }
        }

		public static void HandleIndexNotFound(string name)
		{
			if (string.IsNullOrWhiteSpace(name) == false)
			{
				var notification = new Notification(string.Format("Could not find '{0}' index", name), NotificationLevel.Warning);
				ApplicationModel.Current.AddNotification(notification);
			}
			UrlUtil.Navigate("/indexes");
		}

		private void ResetToOriginal()
		{
			index = JsonConvert.DeserializeObject<IndexDefinition>(originalIndex);
			UpdateFromIndex(index);
		}

		private void UpdateIndex()
		{
			index.Map = Maps.Select(x => x.Text).FirstOrDefault();
			index.Maps = new HashSet<string>(Maps.Select(x => x.Text));
			UpdateFields();
		}

		private void UpdateFields()
		{
			index.Indexes.Clear();
			index.Stores.Clear();
			index.SortOptions.Clear();
			index.Analyzers.Clear();
			foreach (var item in Fields.Where(item => item.Name != null))
			{
				index.Indexes[item.Name] = item.Indexing;
				index.Stores[item.Name] = item.Storage;
				index.SortOptions[item.Name] = item.Sort;
				index.Analyzers[item.Name] = item.Analyzer;
			}
			index.RemoveDefaultValues();
		}

		void CreateOrEditField<T>(IEnumerable<KeyValuePair<string, T>> dictionary, Action<FieldProperties, T> setter)
		{
			if (dictionary == null) return;

			foreach (var item in dictionary)
			{
				var localItem = item;
				var field = Fields.FirstOrDefault(f => f.Name == localItem.Key);
				if (field == null)
				{
					field = FieldProperties.Defualt;
					field.Name = localItem.Key;
					Fields.Add(field);
				}
				setter(field, localItem.Value);
			}
		}	

		public string Name
		{
			get { return index.Name; }
			set
			{
			    if (index.Name != value)
			    {
                    MarkAsDirtyIfSignificant(index.Name, value);
			        index.Name = value;
			        OnPropertyChanged(() => Name);
			    }
			}
		}

	    private void MarkAsDirtyIfSignificant(string oldValue, string newValue)
	    {
	        if (!(string.IsNullOrEmpty(oldValue) && string.IsNullOrEmpty(newValue)))
	        {
	            MarkAsDirty();
	        }
	    }

	    //public string MapUrl
		//{
		//    get{return }
		//}

		private string header;
		public string Header
		{
			get { return header; }
			set
			{
				header = value;
				OnPropertyChanged(() => Header);
			}
		}

		private bool showReduce;
		public bool ShowReduce
		{
			get { return showReduce; }
			set
			{
				showReduce = value;
				OnPropertyChanged(() => ShowReduce);
			}
		}

		public string Reduce
		{
			get { return index.Reduce; }
			set
			{
			    if (index.Reduce != value)
			    {
			        MarkAsDirtyIfSignificant(index.Reduce, value);
			        index.Reduce = value;
			        OnPropertyChanged(() => Reduce);
			    }
			}
		}

	    private void MarkAsDirty()
	    {
	        hasUnsavedChanges = true;
	    }

	    private bool showTransformResults;
		public bool ShowTransformResults
		{
			get { return showTransformResults; }
			set
			{
				showTransformResults = value;
				OnPropertyChanged(() => ShowTransformResults);
			}
		}

		public string TransformResults
		{
			get { return index.TransformResults; }
			set
			{
			    if (index.TransformResults != value)
			    {
			        MarkAsDirtyIfSignificant(index.TransformResults, value);
			        index.TransformResults = value;
			        OnPropertyChanged(() => TransformResults);
			    }
			}
		}

		public BindableCollection<MapItem> Maps { get; private set; }
		public BindableCollection<FieldProperties> Fields { get; private set; }

		public int ErrorsCount
		{
			get
			{
				var databaseStatistics = statistics.Value;
				return databaseStatistics == null ? 0 : databaseStatistics.Errors.Count();
			}
		}

#region Commands

		public ICommand AddMap
		{
			get { return new ChangeFieldValueCommand<IndexDefinitionModel>(this, x => x.Maps.Add(new MapItem())); }
		}

		public ICommand RemoveMap
		{
			get { return new RemoveMapCommand(this); }
		}

		public ICommand AddReduce
		{
			get { return new ChangeFieldValueCommand<IndexDefinitionModel>(this, x => x.ShowReduce = true); }
		}

		public ICommand RemoveReduce
		{
			get { return new ChangeFieldValueCommand<IndexDefinitionModel>(this, x => x.ShowReduce = false); }
		}

		public ICommand AddTransformResults
		{
			get { return new ChangeFieldValueCommand<IndexDefinitionModel>(this, x => x.ShowTransformResults = true); }
		}

		public ICommand RemoveTransformResults
		{
			get { return new ChangeFieldValueCommand<IndexDefinitionModel>(this, x => x.ShowTransformResults = false); }
		}

		public ICommand AddField
		{
			get { return new ChangeFieldValueCommand<IndexDefinitionModel>(this, x => x.Fields.Add(FieldProperties.Defualt)); }
		}

		public ICommand RemoveField
		{
			get { return new RemoveFieldCommand(this); }
		}

		public ICommand SaveIndex
		{
			get { return new SaveIndexCommand(this); }
		}

		public ICommand DeleteIndex
		{
			get { return new DeleteIndexCommand(this); }
		}

		public ICommand ResetIndex
		{
			get { return new ResetIndexCommand(this); }
		}

		private class RemoveMapCommand : Command
		{
			private readonly IndexDefinitionModel index;

			public RemoveMapCommand(IndexDefinitionModel index)
			{
				this.index = index;
			}

			public override void Execute(object parameter)
			{
				var map = parameter as MapItem;
				if (map == null || index.Maps.Contains(map) == false)
					return;

				index.Maps.Remove(map);
			}
		}

		private class RemoveFieldCommand : Command
		{
			private FieldProperties field;
			private readonly IndexDefinitionModel index;

			public RemoveFieldCommand(IndexDefinitionModel index)
			{
				this.index = index;
			}

			public override bool CanExecute(object parameter)
			{
				field = parameter as FieldProperties;
				return field != null && index.Fields.Contains(field);
			}

			public override void Execute(object parameter)
			{
				index.Fields.Remove(field);
			}
		}

		private class SaveIndexCommand : Command
		{
			private readonly IndexDefinitionModel index;

			public SaveIndexCommand(IndexDefinitionModel index)
			{
				this.index = index;
			}

			public override void Execute(object parameter)
			{
				if (string.IsNullOrWhiteSpace(index.Name))
				{
					ApplicationModel.Current.AddNotification(new Notification("Index must have a name!", NotificationLevel.Error));
					return;
				}
				if (index.Maps.All(item => string.IsNullOrWhiteSpace(item.Text)))
				{
					ApplicationModel.Current.AddNotification(new Notification("Index must have at least one map with data!", NotificationLevel.Error));
					return;
				}
				index.UpdateIndex();
				if (index.Reduce == "")
					index.Reduce = null;
				if (index.TransformResults == "" || index.ShowTransformResults == false)
					index.TransformResults = null;

				var mapIndexes = (from mapItem in index.Maps where mapItem.Text == "" select index.Maps.IndexOf(mapItem)).ToList();
				mapIndexes.Sort();

				for (int i = mapIndexes.Count - 1; i >= 0; i--)
				{
					index.Maps.RemoveAt(mapIndexes[i]);
				}

					ApplicationModel.Current.AddNotification(new Notification("saving index " + index.Name));
				DatabaseCommands.PutIndexAsync(index.Name, index.index, true)
					.ContinueOnSuccess(() =>
					                       {
					                           ApplicationModel.Current.AddNotification(
					                               new Notification("index " + index.Name + " saved"));
					                           index.hasUnsavedChanges = false;
					                           PutIndexNameInUrl(index.Name);
					                       })
					.Catch();
			}

			private void PutIndexNameInUrl(string name)
			{
				if (index.IsNewIndex || index.Header != name)
				{
					UrlUtil.Navigate("/indexes/" + name, true);
				}
			}
		}

		private class ResetIndexCommand : Command
		{
			private readonly IndexDefinitionModel index;

			public ResetIndexCommand(IndexDefinitionModel index)
			{
				this.index = index;
			}

			public override void Execute(object parameter)
			{
				ApplicationModel.Current.AddNotification(new Notification("resetting index " + index.Name));
				index.ResetToOriginal();
				ApplicationModel.Current.AddNotification(new Notification("index " + index.Name + " was reset"));
			}
		}

		private class DeleteIndexCommand : Command
		{
			private readonly IndexDefinitionModel index;

			public DeleteIndexCommand(IndexDefinitionModel index)
			{
				this.index = index;
			}

			public override bool CanExecute(object parameter)
			{
				return index != null && index.IsNewIndex == false;
			}

			public override void Execute(object parameter)
			{
				AskUser.ConfirmationAsync("Confirm Delete", "Really delete '" + index.Name + "' index?")
					.ContinueWhenTrue(DeleteIndex);
			}

			private void DeleteIndex()
			{
				DatabaseCommands
					.DeleteIndexAsync(index.Name)
			        .ContinueOnUIThread(t =>
					                                	{
			                                    if (t.IsFaulted)
			                                    {
					                                		ApplicationModel.Current.AddErrorNotification(t.Exception,"index " + index.Name +" could not be deleted");
			                                    }
			                                    else
			                                    {
			                                        ApplicationModel.Current.AddInfoNotification("index " + index.Name +" successfully deleted");
					                                		UrlUtil.Navigate("/indexes");
			                                    }
					                                	});
			}
		}

		#endregion Commands

		public class MapItem : NotifyPropertyChangedBase
		{
		    private string text;

		    public string Text
		    {
		        get { return text; }
                set
                {
                    if (text != value)
                    {
                        text = value;
                        OnPropertyChanged(() => Text);
                    }
                }
		    }
		}

		public class FieldProperties : NotifyPropertyChangedBase
		{
		    private string name;
		    public string Name
		    {
		        get { return name; }
                set
                {
                    if (name != value)
                    {
                        name = value;
                        OnPropertyChanged(() => Name);
                    }
                }
		    }

		    private FieldStorage storage;
		    public FieldStorage Storage
		    {
		        get { return storage; }
		        set {
		            if (storage != value)
		            {
		                storage = value;
		                OnPropertyChanged(() => Storage);
		            }
                }
		    }

		    private FieldIndexing indexing;
		    public FieldIndexing Indexing
		    {
		        get { return indexing; }
		        set {
		            if (indexing != value)
		            {
		                indexing = value;
		                OnPropertyChanged(() => Indexing);
		            }
                }
		    }

		    private SortOptions sort;
		    public SortOptions Sort
		    {
		        get { return sort; }
                set
                {
                    if (sort != value)
                    {
                        sort = value;
                        OnPropertyChanged(() => Sort);
                    }
                }
		    }

		    private string analyzer;
		    public string Analyzer
		    {
		        get { return analyzer; }
		        set {
		            if (analyzer != value)
		            {
		                analyzer = value;
		                OnPropertyChanged(() => Analyzer);
		            }
		        }
		    }

		    public static FieldProperties Defualt
			{
				get
				{
					return new FieldProperties
					{
						Storage = FieldStorage.No,
						Indexing = FieldIndexing.Default,
						Sort = SortOptions.None,
						Analyzer = string.Empty
					};
				}
			}
		}

		public string PageTitle
		{
			get { return "Edit Index"; }
		}

		public bool IsNewIndex
		{
			get { return isNewIndex; }
			set
			{
				isNewIndex = value;
				OnPropertyChanged(() => IsNewIndex);
			}
		}
	}
}
