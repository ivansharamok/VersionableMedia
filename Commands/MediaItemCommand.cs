using System;
using System.Collections.Generic;
using System.Text;
using Sitecore.Data.Items;
using Sitecore.Shell.Framework.Commands;
using Custom.Media.Conversion;

namespace Custom.Media.Commands
{
   public class MediaItemCommand : Sitecore.Shell.Framework.Commands.Command
   {
      public override void Execute(Sitecore.Shell.Framework.Commands.CommandContext context)
      {
         if (context.Items.Length == 1)
         {
            Item item = context.Items[0];            
            UnversionedToVersioned utv = new UnversionedToVersioned();            
            utv.MigrateItem(item);
         }
      }

      public override CommandState QueryState(CommandContext context)
      {
         if (context.Items.Length != 1)
         {
            return CommandState.Hidden;
         }
         Item item = context.Items[0];
         if (!item.Access.CanWrite())
         {
            return CommandState.Disabled;
         }
         return base.QueryState(context);
      }
   }
}
