using System;
using System.Collections.Generic;
using System.Text;

using Sitecore.Data.Items;
using Sitecore.Data.DataProviders.Sql;
using Sitecore.Data;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Configuration;
using Sitecore.Data.Managers;
using Sitecore.Globalization;

namespace Custom.Media.Conversion
{
   public class UnversionedToVersioned
   {
      protected static readonly string sharedBaseTemplateID = "{962B53C4-F93B-4DF9-9821-415C867B8903}";
      protected static readonly string versionedBaseTemplateID = "{611933AC-CE0C-4DDC-9683-F830232DB150}";

      protected static readonly Database database;

      protected static readonly Item sharedParentNode;
      protected static readonly Item versionedParentNode;

      //static readonly string cleanVersionedFields = " DELETE FROM [VersionedFields] WHERE [FieldId] = @fieldId AND [ItemId] = @itemId";
      static readonly string copyFromShared = " INSERT INTO [VersionedFields]( [ItemId], [Language], [Version], [FieldId], [Value], [Blob], [Created], [Updated] )" +
         " SELECT [ItemId], @language, @version, @fieldId, [Value], [Blob], [Created], [Updated] FROM [SharedFields] f " +
         "WHERE [Id] IN (   SELECT TOP 1 [Id]   FROM [SharedFields]   WHERE [ItemId] = @itemId   AND [FieldId] = @fieldId " +
         "ORDER BY [Updated] DESC ) AND EXISTS(   SELECT [Id]   FROM [VersionedFields] " +
         "WHERE [ItemId] = @itemId   AND [Language] = @language   AND [Version] = @version)";
      static readonly string sqlDeleteShared = " DELETE FROM [SharedFields] WHERE [FieldId] = @fieldId AND [ItemId] = @itemId";

      static UnversionedToVersioned()
      {
         database = Factory.GetDatabase("master");
         sharedParentNode = database.GetItem(ID.Parse(sharedBaseTemplateID)).Parent;
         versionedParentNode = database.GetItem(ID.Parse(versionedBaseTemplateID)).Parent;
      }

      static List<TemplateField> GetTemplateFields()
      {
         List<TemplateField> list = new List<TemplateField>();

         foreach (Item sharedTemplateInnerItem in sharedParentNode.Children)
         {
            TemplateItem sharedTemplateItem = new TemplateItem(sharedTemplateInnerItem);
            TemplateItem versionedTemplateItem = GetVersionedMediaTemplateItem(sharedTemplateItem);

            Template sharedTemplate = TemplateManager.GetTemplate(sharedTemplateInnerItem.ID, database);
            Template versionedTemplate = TemplateManager.GetTemplate(versionedTemplateItem.ID, database);

            TemplateField[] sharedFields = sharedTemplate.GetFields();
            TemplateField[] versionedFields = versionedTemplate.GetFields();

            foreach (TemplateField sharedField in sharedFields)
            {
               TemplateField versionedField =
                   Array.Find(versionedFields,
                              delegate(TemplateField field) { return field.Name == sharedField.Name; });

               if (sharedField.IsShared && !sharedField.IsUnversioned && !versionedField.IsShared && !versionedField.IsUnversioned)
               {
                  if (list.Find(delegate(TemplateField field) { return field.ID == sharedField.ID; }) == null)
                  {
                     list.Add(versionedField);
                  }
               }
            }
         }

         return list;
      }

      public void MigrateItem(Item item)
      {
         if (ShouldMigrate(database.GetTemplate(item.TemplateID)))
         {
            TemplateItem oldTemplate = database.GetTemplate(item.TemplateID);
            TemplateItem newTemplate = GetVersionedMediaTemplateItem(oldTemplate);

            item.ChangeTemplate(newTemplate);
            Log.Info("Item modified: " + item.Paths.FullPath, this);
            Log.Info("Source template: " + oldTemplate.InnerItem.Paths.FullPath, this);
            Log.Info("Target template: " + newTemplate.InnerItem.Paths.FullPath, this);
            MakeVersionedFields(item);
         }
         else
         {
            Log.Info("Item skipped: " + item.Paths.FullPath, this);
         }
      }

      private void MakeVersionedFields(Item mediaItem)
      {
         Sitecore.Data.SqlServer.SqlServerApi api = new Sitecore.Data.SqlServer.SqlServerApi(Sitecore.Configuration.Factory.GetString("connections/master", true));
         Language[] languages = mediaItem.Languages;

         using (DataProviderTransaction transaction = api.CreateTransactionScope())
         {
            foreach (TemplateField field in GetTemplateFields())
            {
               //api.Execute(cleanVersionedFields, new object[] { "fieldId", field.ID, "itemId", mediaItem.ID });
               foreach (Language language in languages)
               {
                  using (new LanguageSwitcher(language))
                  {
                     Sitecore.Data.Version[] versions = mediaItem.Versions.GetVersionNumbers();

                     foreach (Sitecore.Data.Version version in versions)
                     {
                        api.Execute(copyFromShared, new object[] { "language", language, "version", version, "fieldId", field.ID, "itemId", mediaItem.ID });
                     }
                  }
               }

               api.Execute(sqlDeleteShared, new object[] { "fieldId", field.ID, "itemId", mediaItem.ID });
            }
            transaction.Complete();
         }
      }

      protected virtual bool ShouldMigrate(TemplateItem template)
      {
         foreach (TemplateItem baseTemplate in template.BaseTemplates)
         {
            if (string.Equals(baseTemplate.ID.ToString(), sharedBaseTemplateID) || ShouldMigrate(baseTemplate))
            {
               return true;
            }
         }
         return false;
      }

      protected static TemplateItem GetVersionedMediaTemplateItem(TemplateItem sharedMediaTemplate)
      {
         string templatePath = string.Concat(versionedParentNode.Paths.FullPath, "/", sharedMediaTemplate.Name);
         return new TemplateItem(database.GetItem(templatePath));
      }
   }
}