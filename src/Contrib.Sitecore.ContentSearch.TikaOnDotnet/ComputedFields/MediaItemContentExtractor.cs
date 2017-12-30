using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Sitecore.Configuration;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.ComputedFields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Reflection;

namespace Contrib.Sitecore.ContentSearch.TikaOnDotnet.ComputedFields
{
    public class MediaItemContentExtractor : AbstractComputedIndexField
    {
        protected static readonly ConcurrentDictionary<string, IComputedIndexField> MimeTypeComputedFields =
            new ConcurrentDictionary<string, IComputedIndexField>();

        protected static readonly ConcurrentDictionary<string, IComputedIndexField> ExtensionComputedFields =
            new ConcurrentDictionary<string, IComputedIndexField>();

        protected static readonly ConcurrentBag<IComputedIndexField> FallbackComputedIndexFields = new ConcurrentBag<IComputedIndexField>();

        protected readonly List<XmlNode> ExtensionIncludes;

        protected readonly List<XmlNode> ExtensionExcludes;

        protected readonly List<XmlNode> MimeTypeIncludes;

        protected readonly List<XmlNode> MimeTypeExcludes;

        private AbstractComputedIndexField _extractor;

        protected AbstractComputedIndexField Extractor
        {
            get => _extractor ?? (_extractor = new MediaItemTikaOnDotNetTextExtractor());
            set => _extractor = value ?? new MediaItemTikaOnDotNetTextExtractor();
        }

        public MediaItemContentExtractor()
            : this(null)
        {
        }

        public MediaItemContentExtractor(XmlNode configurationNode)
        {
            ExtensionExcludes = new List<XmlNode>();
            ExtensionIncludes = new List<XmlNode>();
            MimeTypeExcludes = new List<XmlNode>();
            MimeTypeIncludes = new List<XmlNode>();
            Initialize(configurationNode);
        }

        public override object ComputeFieldValue(IIndexable indexable)
        {
            Item item = indexable as SitecoreIndexableItem;

            if (item == null) return null;

            IComputedIndexField computedField;

            var mimeTypeField = item.Fields["Mime Type"];

            if (!string.IsNullOrEmpty(mimeTypeField?.Value))
            {
                if (MimeTypeComputedFields.TryGetValue(mimeTypeField.Value.ToLowerInvariant(), out computedField))
                {
                    return computedField.ComputeFieldValue((SitecoreIndexableItem) item);
                }
            }

            var extensionField = item.Fields["Extension"];

            if (!string.IsNullOrEmpty(extensionField?.Value))
            {
                if (ExtensionComputedFields.TryGetValue(extensionField.Value.ToLowerInvariant(), out computedField))
                {
                    return computedField.ComputeFieldValue((SitecoreIndexableItem) item);
                }
            }

            foreach (var fallback in FallbackComputedIndexFields)
            {
                if (mimeTypeField != null && extensionField != null && (ExtensionExcludes.Select(node => node.InnerText)
                                                                            .Contains(extensionField.Value.ToLowerInvariant()) ||
                                                                        MimeTypeExcludes.Select(node => node.InnerText)
                                                                            .Contains(mimeTypeField.Value.ToLowerInvariant())))
                {
                    return null;
                }

                var value = fallback.ComputeFieldValue((SitecoreIndexableItem) item);

                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        public static IEnumerable<T> Transform<T>(System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object>().Where(current => (current as XmlElement) != null).Cast<T>();
        }

        private void AddMediaItemContentExtractorByMimeType(string mimeType, IComputedIndexField computedField)
        {
            Assert.ArgumentNotNull(mimeType, "mimeType");
            Assert.ArgumentNotNull(computedField, "computedField");

            MimeTypeComputedFields[mimeType] = computedField;
        }

        private void AddMediaItemContentExtractorByFileExtension(string extension, IComputedIndexField computedField)
        {
            Assert.ArgumentNotNull(extension, "extension");
            Assert.ArgumentNotNull(computedField, "computedField");

            ExtensionComputedFields[extension] = computedField;
        }

        private void AddFallbackMediaItemContentExtractor(IComputedIndexField computedField)
        {
            Assert.ArgumentNotNull(computedField, "computedField");
            FallbackComputedIndexFields.Add(computedField);
        }

        public void Initialize(XmlNode configurationNode)
        {
            if (configurationNode == null)
            {
                return;
            }

            XmlNode mediaIndexing = null;
            if (configurationNode.ChildNodes.Count > 0)
            {
                mediaIndexing = configurationNode.SelectSingleNode("mediaIndexing");
                if (mediaIndexing?.Attributes?["ref"] != null)
                {
                    var mediaIndexingLocation = mediaIndexing.Attributes["ref"].Value;
                    if (string.IsNullOrEmpty(mediaIndexingLocation))
                    {
                        Log.Error(
                            "<mediaIndexing> configuration error: \"ref\" attribute in mediaindexing section cannot be empty.",
                            this);
                        return;
                    }

                    mediaIndexing = Factory.GetConfigNode(mediaIndexingLocation);
                }
            }

            if (mediaIndexing == null)
            {
                Log.Error("Could not find <mediaIndexing> node in content search index configuration.", this);
                return;
            }

            var node = mediaIndexing.SelectSingleNode("extensions/includes");
            if (node == null)
            {
                Log.Error("<mediaIndexing> configuration error: \"extensions/includes\" node not found.", this);
            }
            else
            {
                ExtensionIncludes.AddRange(Transform<XmlNode>(node.ChildNodes));
            }

            node = mediaIndexing.SelectSingleNode("extensions/excludes");
            if (node == null)
            {
                Log.Error("<mediaIndexing> configuration error: \"extensions/excludes\" node not found.", this);
            }
            else
            {
                ExtensionExcludes.AddRange(Transform<XmlNode>(node.ChildNodes));
            }

            if (ExtensionExcludes.Count == 1)
            {
                if (ExtensionExcludes.First().InnerText == "*")
                {
                    foreach (var extensionInculde in ExtensionIncludes)
                    {
                        if (extensionInculde.Attributes?["type"] != null)
                        {
                            AddMediaItemContentExtractorByFileExtension(extensionInculde.InnerText,
                                ReflectionUtil.CreateObject(extensionInculde.Attributes["type"].Value) as
                                    IComputedIndexField);
                        }
                        else
                        {
                            AddMediaItemContentExtractorByFileExtension(extensionInculde.InnerText, Extractor);
                        }
                    }
                }
            }
            else if (ExtensionIncludes.Count == 1)
            {
                if (ExtensionIncludes.First().InnerText == "*")
                {
                    AddFallbackMediaItemContentExtractor(Extractor);
                }
            }
            else
            {
                foreach (var extensionInculde in ExtensionIncludes)
                {
                    if (extensionInculde.Attributes?["type"] != null)
                    {
                        AddMediaItemContentExtractorByFileExtension(extensionInculde.InnerText,
                            ReflectionUtil.CreateObject(extensionInculde.Attributes["type"].Value) as
                                IComputedIndexField);
                    }
                    else
                    {
                        AddMediaItemContentExtractorByFileExtension(extensionInculde.InnerText, Extractor);
                    }
                }
            }

            node = mediaIndexing.SelectSingleNode("mimeTypes/includes");
            if (node == null)
            {
                Log.Error("<mediaIndexing> configuration error: \"mimeTypes/includes\" node not found.", this);
            }
            else
            {
                MimeTypeIncludes.AddRange(Transform<XmlNode>(node.ChildNodes));
            }

            node = mediaIndexing.SelectSingleNode("mimeTypes/excludes");
            if (node == null)
            {
                Log.Error("<mediaIndexing> configuration error: \"mimeTypes/excludes\" node not found.", this);
            }
            else
            {
                MimeTypeExcludes.AddRange(Transform<XmlNode>(node.ChildNodes));
            }

            if (MimeTypeExcludes.Count == 1)
            {
                if (MimeTypeExcludes.First().InnerText == "*")
                {
                    foreach (var mimeTypeInclude in MimeTypeIncludes)
                    {
                        if (mimeTypeInclude.Attributes?["type"] != null)
                        {
                            AddMediaItemContentExtractorByMimeType(mimeTypeInclude.InnerText,
                                ReflectionUtil.CreateObject(mimeTypeInclude.Attributes["type"].Value) as
                                    IComputedIndexField);
                        }
                        else
                        {
                            AddMediaItemContentExtractorByMimeType(mimeTypeInclude.InnerText, Extractor);
                        }
                    }
                }
            }
            else if (MimeTypeIncludes.Count == 1)
            {
                if (MimeTypeIncludes.First().InnerText == "*")
                {
                    AddFallbackMediaItemContentExtractor(Extractor);
                }
            }
            else
            {
                foreach (var mimeTypeInclude in MimeTypeIncludes)
                {
                    if (mimeTypeInclude.Attributes?["type"] != null)
                    {
                        AddMediaItemContentExtractorByMimeType(mimeTypeInclude.InnerText,
                            ReflectionUtil.CreateObject(
                                    mimeTypeInclude.Attributes["type"].Value) as
                                IComputedIndexField);
                    }
                    else
                    {
                        AddMediaItemContentExtractorByMimeType(mimeTypeInclude.InnerText, Extractor);
                    }
                }
            }
        }
    }
}