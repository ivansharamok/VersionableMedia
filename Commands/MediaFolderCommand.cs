using System;
using System.Collections.Generic;
using System.Text;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Data.Items;
using Sitecore;
using Custom.Media.Conversion;

namespace Custom.Media.Commands
{
   public class MediaFolderCommand : Sitecore.Shell.Framework.Commands.Command
   {
      private int itemsProcessed = 0;

      public override void Execute(Sitecore.Shell.Framework.Commands.CommandContext context)
      {
         if (context.Items.Length == 1)
         {
            Item item = context.Items[0];
            MediaItem mediaFolderItem = item;

            Migrate(mediaFolderItem);

            Sitecore.Context.ClientPage.ClientResponse.Alert("Conversion Done. Items processed: " + itemsProcessed.ToString());

            itemsProcessed = 0;
         }
      }

      public override CommandState QueryState(CommandContext context)
      {
         if (context.Items.Length != 1)
         {
            return CommandState.Hidden;
         }
         Item item = context.Items[0];
         if (!item.Access.CanCreate())
         {
            return CommandState.Disabled;
         }
         return base.QueryState(context);
      }

      private void Migrate(Item parentItem)
      {
         UnversionedToVersioned utv = new UnversionedToVersioned();
         foreach (Item childMediaItem in parentItem.Children)
         {
            if (childMediaItem.TemplateID != TemplateIDs.MediaFolder)
            {
               if (childMediaItem.TemplateID != TemplateIDs.Folder)
               {
                  itemsProcessed++;
                  utv.MigrateItem(childMediaItem);
               }
            }
            else
            {
               Sitecore.Diagnostics.Log.Warn("MediaConversion: Cannot change unversioned media tamplate to versioned for " + childMediaItem.Paths.FullPath, this);
            }

            Migrate(childMediaItem);
         }
      }
   }
}
