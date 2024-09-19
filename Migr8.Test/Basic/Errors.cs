﻿using Migr8.Internals;
using NUnit.Framework;

namespace Migr8.Test.Basic
{
    [TestFixture]
    public class Errors : DbFixtureBase
    {
        DatabaseMigratorCore _migrator;

        protected override void SetUp()
        {
            base.SetUp();
            _migrator = new DatabaseMigratorCore(new ThreadPrintingConsoleWriter(), TestConfig.ConnectionString);
        }

        [Test]
        public void GivesNiceErrorMessage()
        {
            const string failSql = "THIS ONE CANNOT BE EXECUTED!";

            var migrations = new[]
            {
                new TestMigration(1, "test", "CREATE TABLE [Table1] ([Id] int)"),
                new TestMigration(2, "test", failSql)
            };

            var ex = Assert.Throws<MigrationException>(() => _migrator.Execute(migrations));

            Assert.That(ex.Message, Contains.Substring("2-test"));
            Assert.That(ex.Message, Contains.Substring(failSql));
        }
    }
}