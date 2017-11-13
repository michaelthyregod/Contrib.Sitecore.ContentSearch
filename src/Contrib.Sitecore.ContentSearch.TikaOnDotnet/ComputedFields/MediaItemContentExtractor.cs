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
        private readonly Dictionary<string, IComputedIndexField> _mimeTypeComputedFields =
            new Dictionary<string, IComputedIndexField>();

        private readonly Dictionary<string, IComputedIndexField> _extensionComputedFields =
            new Dictionary<string, IComputedIndexField>();

        private readonly List<IComputedIndexField> _fallbackComputedIndexFields = new List<IComputedIndexField>();

        private readonly List<XmlNode> _extensionIncludes;

        private readonly List<XmlNode> _extensionExcludes;

        private readonly List<XmlNode> _mimeTypeIncludes;

        private readonly List<XmlNode> _mimeTypeExcludes;

        public MediaItemContentExtractor()
            : this(null)
        {
        }

        public MediaItemContentExtractor(XmlNode configurationNode)
        {
            _extensionExcludes = new List<XmlNode>();
            _extensionIncludes = new List<XmlNode>();
            _mimeTypeExcludes = new List<XmlNode>();
            _mimeTypeIncludes = new List<XmlNode>();
            Initialize(configurationNode);
        }

        public override object ComputeFieldValue(IIndexable indexable)
        {
            Item item = indexable as SitecoreIndexableItem;

            if (item == null) return null;

            IComputedIndexField computedField;

            var mimeTypeField = item.Fields["Mime Type"];

            if (mimeTypeField != null && !string.IsNullOrEmpty(mimeTypeField.Value))
            {
                if (_mimeTypeComputedFields.TryGetValue(mimeTypeField.Value.ToLowerInvariant(), out computedField))
                {
                    return computedField.ComputeFieldValue((SitecoreIndexableItem) item);
                }
            }

            var extensionField = item.Fields["Extension"];

            if (extensionField != null && !string.IsNullOrEmpty(extensionField.Value))
            {
                if (_extensionComputedFields.TryGetValue(extensionField.Value.ToLowerInvariant(), out computedField))
                {
                    return computedField.ComputeFieldValue((SitecoreIndexableItem) item);
                }
            }

            foreach (var fallback in _fallbackComputedIndexFields)
            {
                if (mimeTypeField != null && extensionField != null && (_extensionExcludes.Select(node => node.InnerText)
                                                                            .Contains(extensionField.Value.ToLowerInvariant()) ||
                                                                        _mimeTypeExcludes.Select(node => node.InnerText)
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

        protected void AddMediaItemContentExtractorByMimeType(string mimeType, IComputedIndexField computedField)
        {
            Assert.ArgumentNotNull(mimeType, "mimeType");
            Assert.ArgumentNotNull(computedField, "computedField");

            _mimeTypeComputedFields[mimeType] = computedField;
        }

        protected void AddMediaItemContentExtractorByFileExtension(string extension, IComputedIndexField computedField)
        {
            Assert.ArgumentNotNull(extension, "extension");
            Assert.ArgumentNotNull(computedField, "computedField");

            _extensionComputedFields[extension] = computedField;
        }

        protected void AddFallbackMediaItemContentExtractor(IComputedIndexField computedField)
        {
            Assert.ArgumentNotNull(computedField, "computedField");

            _fallbackComputedIndexFields.Insert(0, computedField);
        }

        protected virtual void Initialize()
        {
            Initialize(null);
        }

        protected virtual void Initialize(XmlNode configurationNode)
        {
            if (configurationNode == null)
            {
                return;
            }

            var extractor = new MediaItemTikaOnDotNetTextExtractor();

            XmlNode mediaIndexing = null;
            if (configurationNode.ChildNodes.Count > 0)
            {
                mediaIndexing = configurationNode.SelectSingleNode("mediaIndexing");
                if (mediaIndexing != null)
                {
                    if (mediaIndexing.Attributes != null && mediaIndexing.Attributes["ref"] != null)
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
                _extensionIncludes.AddRange(Transform<XmlNode>(node.ChildNodes));
            }

            node = mediaIndexing.SelectSingleNode("extensions/excludes");
            if (node == null)
            {
                Log.Error("<mediaIndexing> configuration error: \"extensions/excludes\" node not found.", this);
            }
            else
            {
                _extensionExcludes.AddRange(Transform<XmlNode>(node.ChildNodes));
            }

            if (_extensionExcludes.Count == 1)
            {
                if (_extensionExcludes.First().InnerText == "*")
                {
                    foreach (var extensionInculde in _extensionIncludes)
                    {
                        if (extensionInculde.Attributes != null && extensionInculde.Attributes["type"] != null)
                        {
                            AddMediaItemContentExtractorByFileExtension(extensionInculde.InnerText,
                                ReflectionUtil.CreateObject(extensionInculde.Attributes["type"].Value) as
                                    IComputedIndexField);
                        }
                        else
                        {
                            AddMediaItemContentExtractorByFileExtension(extensionInculde.InnerText, extractor);
                        }
                    }
                }
            }
            else if (_extensionIncludes.Count == 1)
            {
                if (_extensionIncludes.First().InnerText == "*")
                {
                    AddFallbackMediaItemContentExtractor(extractor);
                }
            }
            else
            {
                foreach (var extensionInculde in _extensionIncludes)
                {
                    if (extensionInculde.Attributes != null && extensionInculde.Attributes["type"] != null)
                    {
                        AddMediaItemContentExtractorByFileExtension(extensionInculde.InnerText,
                            ReflectionUtil.CreateObject(extensionInculde.Attributes["type"].Value) as
                                IComputedIndexField);
                    }
                    else
                    {
                        AddMediaItemContentExtractorByFileExtension(extensionInculde.InnerText, extractor);
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
                _mimeTypeIncludes.AddRange(Transform<XmlNode>(node.ChildNodes));
            }

            node = mediaIndexing.SelectSingleNode("mimeTypes/excludes");
            if (node == null)
            {
                Log.Error("<mediaIndexing> configuration error: \"mimeTypes/excludes\" node not found.", this);
            }
            else
            {
                _mimeTypeExcludes.AddRange(Transform<XmlNode>(node.ChildNodes));
            }

            if (_mimeTypeExcludes.Count == 1)
            {
                if (_mimeTypeExcludes.First().InnerText == "*")
                {
                    foreach (var mimeTypeInclude in _mimeTypeIncludes)
                    {
                        if (mimeTypeInclude.Attributes != null && mimeTypeInclude.Attributes["type"] != null)
                        {
                            AddMediaItemContentExtractorByMimeType(mimeTypeInclude.InnerText,
                                ReflectionUtil.CreateObject(mimeTypeInclude.Attributes["type"].Value) as
                                    IComputedIndexField);
                        }
                        else
                        {
                            AddMediaItemContentExtractorByMimeType(mimeTypeInclude.InnerText, extractor);
                        }
                    }
                }
            }
            else if (_mimeTypeIncludes.Count == 1)
            {
                if (_mimeTypeIncludes.First().InnerText == "*")
                {
                    AddFallbackMediaItemContentExtractor(extractor);
                }
            }
            else
            {
                foreach (var mimeTypeInclude in _mimeTypeIncludes)
                {
                    if (mimeTypeInclude.Attributes != null && mimeTypeInclude.Attributes["type"] != null)
                    {
                        AddMediaItemContentExtractorByMimeType(mimeTypeInclude.InnerText,
                            ReflectionUtil.CreateObject(
                                    mimeTypeInclude.Attributes["type"].Value) as
                                IComputedIndexField);
                    }
                    else
                    {
                        AddMediaItemContentExtractorByMimeType(mimeTypeInclude.InnerText, extractor);
                    }
                }
            }
        }
    }
}