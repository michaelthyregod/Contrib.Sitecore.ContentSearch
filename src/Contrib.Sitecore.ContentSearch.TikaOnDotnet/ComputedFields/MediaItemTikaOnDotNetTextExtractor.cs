using Sitecore.ContentSearch;
using Sitecore.ContentSearch.ComputedFields;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.IO;
using Sitecore.Resources.Media;
using System;
using System.IO;
using TikaOnDotNet.TextExtraction;

namespace Contrib.Sitecore.ContentSearch.TikaOnDotnet.ComputedFields
{
    public class MediaItemTikaOnDotNetTextExtractor : AbstractComputedIndexField
    {
        private MediaProvider _mediaProvider;
        private string _mediaIndexingFolder;
        private TextExtractor _textExtractor;

        protected MediaProvider MediaProvider
        {
            get => _mediaProvider ?? (_mediaProvider = new MediaProvider());
            set => _mediaProvider = value ?? new MediaProvider();
        }

        protected string MediaIndexingFolder
        {
            get => _mediaIndexingFolder ?? (_mediaIndexingFolder = ContentSearchConfigurationSettingsWrapper.MediaIndexingFolder);
            set => _mediaIndexingFolder = value ?? ContentSearchConfigurationSettingsWrapper.MediaIndexingFolder;
        }

        protected TextExtractor TextExtractor
        {
            get => _textExtractor ?? (_textExtractor = new TextExtractor());
            set => _textExtractor = value ?? new TextExtractor();
        }


        public override object ComputeFieldValue(IIndexable indexable)
        {
            Item item = indexable as SitecoreIndexableItem;
            if (item == null)
            {
                return null;
            }

            var media = MediaProvider.GetMedia(item);

            if (media == null)
            {
                return null;
            }

            if (item.Fields["Extension"] == null)
            {
                return null;
            }

            var mediaIndexingFolder = MediaIndexingFolder;
            var fileName = $"{Guid.NewGuid()}-{item.Name}.{item.Fields["Extension"].Value}";

            var tempFilePath = FileUtil.MakePath(mediaIndexingFolder, fileName);

            try
            {
                if (!Directory.Exists(mediaIndexingFolder))
                {
                    Directory.CreateDirectory(mediaIndexingFolder);
                }

                using (Stream file = File.OpenWrite(tempFilePath))
                {
                    using (var mediaStream = media.GetStream())
                    {
                        if (mediaStream == null)
                        {
                            return null;
                        }

                        CopyStream(mediaStream.Stream, file);
                    }
                }

                try
                {
                    return TextExtractor.Extract(tempFilePath);
                }
                catch (Exception e)
                {
                    Log.SingleWarn(e.Message, this);
                    return null;
                }
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }

        private static void CopyStream(Stream input, Stream output)
        {
            var buffer = new byte[8 * 1024];
            int len;

            while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, len);
            }
        }
    }
}