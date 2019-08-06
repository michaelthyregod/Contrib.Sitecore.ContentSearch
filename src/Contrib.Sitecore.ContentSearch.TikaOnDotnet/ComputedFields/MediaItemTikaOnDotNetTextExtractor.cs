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
        public override object ComputeFieldValue(IIndexable indexable)
        {
            Item item = indexable as SitecoreIndexableItem;
            if (item == null)
            {
                Log.Warn("Item to index is null", this);
                return null;
            }

            var media = MediaManager.GetMedia(item);

            if (media == null)
            {
                Log.Warn("Media to index is null", this);
                return null;
            }

            if (item.Fields["Extension"] == null)
            {
                Log.Warn("Field Extension is null", this);
                return null;
            }

            var mediaIndexingFolder = ContentSearchConfigurationSettingsWrapper.MediaIndexingFolder;
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
                    var textExtractor = new TextExtractor();
                    return textExtractor.Extract(tempFilePath).Text;
                }
                catch (Exception e)
                {
                    Log.Warn(e.Message, this);
                    return null;
                }
            }
            catch (Exception exception)
            {
                Log.Warn(exception.Message, this);
                return null;
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
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