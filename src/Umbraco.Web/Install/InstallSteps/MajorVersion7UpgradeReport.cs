using System;
using System.Collections.Generic;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.Install.Models;

namespace Umbraco.Web.Install.InstallSteps
{
    [InstallSetupStep("MajorVersion7UpgradeReport", 2)]
    internal class MajorVersion7UpgradeReport : InstallSetupStep<object>
    {
        private readonly ApplicationContext _applicationContext;
        private readonly InstallStatusType _status;

        public MajorVersion7UpgradeReport(ApplicationContext applicationContext, InstallStatusType status)
        {
            _applicationContext = applicationContext;
            _status = status;
        }

        public override InstallSetupResult Execute(object model)
        {
            if (_status == InstallStatusType.NewInstall)
            {
                return null;
            }
            //we cannot run this step if the db is not configured.
            if (_applicationContext.DatabaseContext.IsDatabaseConfigured == false)
            {
                return null;
            }

            return new InstallSetupResult("version7upgradereport", CreateReport());
        }
        
        public override bool RequiresExecution()
        {
            if (_status == InstallStatusType.NewInstall)
            {
                return false;
            }

            //we cannot run this step if the db is not configured.
            if (_applicationContext.DatabaseContext.IsDatabaseConfigured == false)
            {
                return false;
            }

            var result = _applicationContext.DatabaseContext.ValidateDatabaseSchema();
            var determinedVersion = result.DetermineInstalledVersion();
            if ((string.IsNullOrWhiteSpace(GlobalSettings.ConfigurationStatus) == false || determinedVersion.Equals(new Version(0, 0, 0)) == false)
                && UmbracoVersion.Current.Major > determinedVersion.Major)
            {
                //it's an upgrade to a major version so we're gonna show this step
                return true;
            }
            return false;
        }

        private IEnumerable<Tuple<bool, string>> CreateReport()
        {
            var errorReport = new List<Tuple<bool, string>>();

            var sql = new Sql();
            sql
                .Select(
                    SqlSyntaxContext.SqlSyntaxProvider.GetQuotedColumn("cmsDataType", "controlId"),
                    SqlSyntaxContext.SqlSyntaxProvider.GetQuotedColumn("umbracoNode", "text"))
                .From(SqlSyntaxContext.SqlSyntaxProvider.GetQuotedTableName("cmsDataType"))
                .InnerJoin(SqlSyntaxContext.SqlSyntaxProvider.GetQuotedTableName("umbracoNode"))
                .On(
                    SqlSyntaxContext.SqlSyntaxProvider.GetQuotedColumn("cmsDataType", "nodeId") + " = " +
                    SqlSyntaxContext.SqlSyntaxProvider.GetQuotedColumn("umbracoNode", "id"));

            var list = _applicationContext.DatabaseContext.Database.Fetch<dynamic>(sql);
            foreach (var item in list)
            {
                Guid legacyId = item.controlId;
                //check for a map entry
                var alias = LegacyPropertyEditorIdToAliasConverter.GetAliasFromLegacyId(legacyId);
                if (alias != null)
                {
                    //check that the new property editor exists with that alias
                    var editor = PropertyEditorResolver.Current.GetByAlias(alias);
                    if (editor == null)
                    {
                        errorReport.Add(new Tuple<bool, string>(false, string.Format("Property Editor with ID '{0}' (assigned to Data Type '{1}') has a valid GUID -> Alias map but no property editor was found. It will be replaced with a Readonly/Label property editor.", item.controlId, item.text)));
                    }
                }
                else
                {
                    errorReport.Add(new Tuple<bool, string>(false, string.Format("Property Editor with ID '{0}' (assigned to Data Type '{1}') does not have a valid GUID -> Alias map. It will be replaced with a Readonly/Label property editor.", item.controlId, item.text)));
                }
            }

            return errorReport;
        }
    }
}