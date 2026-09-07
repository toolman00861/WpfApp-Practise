using SqlSugar;
using System;
using System.IO;
using WpfApp2.Model;
using WpfApp2.Services;

namespace WpfApp2.Database
{
    /// <summary>
    /// 本地库入口，相当于后台那份 IFreeSql 单例。
    /// 文件在 exe 旁边的 app.db：工控机断网也能记检测结果。
    /// </summary>
    public static class Db
    {
        public static SqlSugarClient Client { get; private set; }

        public static void Init()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "app.db");

            Client = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = "Data Source=" + path + ";Version=3;",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            // 没有表就按实体建；有表只补新列，不会清数据。
            AppLog.Info("同步表结构");
            Client.CodeFirst.InitTables(typeof(CellRecord));

            // SQL 走 Debug：练习能在日志文件里看到；现场把 log4net.config 的 root 改成 INFO 就不会刷盘。
            Client.Aop.OnLogExecuting = (sql, pars) =>
            {
                AppLog.Debug(sql);
            };
        }
    }
}

