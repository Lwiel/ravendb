﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FastTests;
using Raven.Client;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Backups;
using Raven.Client.Exceptions;
using Raven.Server.Config;
using Raven.Tests.Core.Utils.Entities;
using Sparrow.Server;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Server.Documents.PeriodicBackup
{
    public class EncryptedBackupTest : RavenTestBase
    {
        public EncryptedBackupTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task backup_encrypted_db_and_restore_to_not_encrypted_DB_with_encrypted_backup_use_db_key_1()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *"
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);

                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = key
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }

        }

        [Fact]
        public async Task backup_encrypted_db_and_restore_to_not_encrypted_DB_with_encrypted_backup_use_db_key_2()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.UseDatabaseKey,
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);

                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = key
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }

        }

        [Fact]
        public async Task backup_encrypted_db_and_restore_to_not_encrypted_DB_with_encrypted_backup_use_new_key()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);

                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Fact]
        public async Task backup_encrypted_db_and_restore_to_encrypted_DB_with_encrypted_backup_use_db_key_1()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *"
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);

                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    EncryptionKey = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = key
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }

        }

        [Fact]
        public async Task backup_encrypted_db_and_restore_to_encrypted_DB_with_encrypted_backup_use_db_key_2()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.UseDatabaseKey,
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);

                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    EncryptionKey = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = key
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }

        }

        [Fact]
        public async Task backup_encrypted_db_and_restore_to_encrypted_DB_with_encrypted_backup_use_new_key()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);

                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    EncryptionKey = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Fact]
        public async Task backup_encrypted_db_and_restore_to_not_encrypted_DB_with_unencrypted_backup()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.None
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;

                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Fact]
        public async Task backup_encrypted_db_and_restore_to_encrypted_DB_with_unencrypted_backup()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.None
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;

                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    EncryptionKey = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs="
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Fact]
        public async Task snapshot_encrypted_db_and_restore_to_encrypted_DB_1()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");

            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Snapshot,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *"
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);

                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    EncryptionKey = key,
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.UseDatabaseKey
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Fact]
        public async Task snapshot_encrypted_db_and_restore_to_encrypted_DB_2()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Snapshot,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.UseDatabaseKey
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);

                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    EncryptionKey = key,
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.UseDatabaseKey
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Fact]
        public async Task backup_not_encrypted_db_and_restore_to_not_encrypted_DB_with_encrypted_backup()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");

            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;

                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Fact]
        public async Task backup_not_encrypted_db_and_restore_to_not_encrypted_DB_with_not_encrypted_backup_1()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");

            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    session.Store(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    session.SaveChanges();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *"
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;

                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Fact]
        public async Task backup_not_encrypted_db_and_restore_to_not_encrypted_DB_with_not_encrypted_backup_2()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");

            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    session.Store(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    session.SaveChanges();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.None
                    }

                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;

                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Fact]
        public async Task backup_not_encrypted_db_and_restore_to_not_encrypted_DB_with_not_encrypted_backup()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");

            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;

                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Fact]
        public async Task backup_unencrypted_db_and_encrypted_backup_fail_2()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.UseDatabaseKey
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    var errorException = getPeriodicBackupResult.Status?.Error.Exception.Contains("InvalidOperationException");
                    return errorException;
                }, true);
            }
        }

        [Fact]
        public async Task can_backup_and_restore_encrypted()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User { Name = "oren" }, "users/1");
                    session.CountersFor("users/1").Increment("likes", 100);
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var status = store.Maintenance.Send(operation).Status;
                    return status?.LastEtag;
                }, 4);
                Assert.Equal(4, value);

                var backupStatus = store.Maintenance.Send(operation);
                var backupOperationId = backupStatus.Status.LastOperationId;

                var backupOperation = store.Maintenance.Send(new GetOperationStateOperation(backupOperationId.Value));

                var backupResult = backupOperation.Result as BackupResult;
                Assert.True(backupResult.Counters.Processed);
                Assert.Equal(1, backupResult.Counters.ReadCount);

                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User { Name = "ayende" }, "users/2");
                    session.CountersFor("users/2").Increment("downloads", 200);

                    await session.SaveChangesAsync();
                }

                var lastEtag = store.Maintenance.Send(new GetStatisticsOperation()).LastDocEtag;
                await store.Maintenance.SendAsync(new StartBackupOperation(false, backupTaskId));
                operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                value = WaitForValue(() => store.Maintenance.Send(operation).Status.LastEtag, lastEtag);
                Assert.Equal(lastEtag, value);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName,
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        Dictionary<string, User> users = new Dictionary<string, User>();
                        var value2 = WaitForValue(() =>
                        {
                            users = session.Load<User>(new[] { "users/2", "users/1" });
                            return users.Count == 2 && users.All(x => x.Value != null);
                        }, true);

                        Assert.True(users.Any(x => x.Value.Name == "oren"));
                        Assert.True(users.Any(x => x.Value.Name == "ayende"));

                        var val = session.CountersFor("users/1").Get("likes");
                        Assert.Equal(100, val);
                        val = session.CountersFor("users/2").Get("downloads");
                        Assert.Equal(200, val);
                    }
                }
            }
        }

        [Fact]
        public async Task can_backup_and_restore_sample_data_encrypted()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");

            using (var store = GetDocumentStore(new Options
            {
                ModifyDatabaseRecord = record => record.Settings[RavenConfiguration.GetKey(x => x.PerformanceHints.MaxNumberOfResults)] = "1"
            }))
            {
                store.Maintenance.Send(new CreateSampleDataOperation());

                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    session.Query<Employee>().ToList(); // this will generate performance hint
                }

                var database = await GetDatabase(store.Database);
                database.NotificationCenter.Paging.UpdatePaging(null);

                int beforeBackupAlertCount;
                using (database.NotificationCenter.GetStored(out var actions))
                    beforeBackupAlertCount = actions.Count();

                Assert.True(beforeBackupAlertCount > 0);

                var beforeBackupStats = store.Maintenance.Send(new GetStatisticsOperation());

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */8 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                };

                var backupTaskId = (store.Maintenance.Send(new UpdatePeriodicBackupOperation(config))).TaskId;
                store.Maintenance.Send(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);

                SpinWait.SpinUntil(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag > 0;
                }, TimeSpan.FromSeconds(15));

                // restore the database with a different name
                var restoredDatabaseName = GetDatabaseName();

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = restoredDatabaseName,
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs="
                    }
                }))
                {
                    var afterRestoreStats = store.Maintenance.ForDatabase(restoredDatabaseName).Send(new GetStatisticsOperation());

                    var restoredDatabase = await GetDatabase(restoredDatabaseName);

                    int afterRestoreAlertCount;
                    using (restoredDatabase.NotificationCenter.GetStored(out var actions))
                        afterRestoreAlertCount = actions.Count();

                    //Assert.True(afterRestoreAlertCount > 0);

                    var indexesPath = restoredDatabase.Configuration.Indexing.StoragePath;
                    var indexesDirectory = new DirectoryInfo(indexesPath.FullPath);
                    Assert.True(indexesDirectory.Exists);
                    Assert.Equal(afterRestoreStats.CountOfIndexes, indexesDirectory.GetDirectories().Length);

                    Assert.NotEqual(beforeBackupStats.DatabaseId, afterRestoreStats.DatabaseId);
                    Assert.Equal(beforeBackupStats.CountOfAttachments, afterRestoreStats.CountOfAttachments);
                    Assert.Equal(beforeBackupStats.CountOfConflicts, afterRestoreStats.CountOfConflicts);
                    Assert.Equal(beforeBackupStats.CountOfDocuments, afterRestoreStats.CountOfDocuments);
                    Assert.Equal(beforeBackupStats.CountOfDocumentsConflicts, afterRestoreStats.CountOfDocumentsConflicts);
                    Assert.Equal(beforeBackupStats.CountOfIndexes, afterRestoreStats.CountOfIndexes);
                    //Assert.Equal(beforeBackupStats.CountOfRevisionDocuments, afterRestoreStats.CountOfRevisionDocuments);
                    Assert.Equal(beforeBackupStats.CountOfTombstones, afterRestoreStats.CountOfTombstones);
                    Assert.Equal(beforeBackupStats.CountOfUniqueAttachments, afterRestoreStats.CountOfUniqueAttachments);
                }
            }
        }

        [Fact]
        public unsafe void failed_to_restore_backup_wrong_key()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    session.Store(new User { Name = "oren" }, "users/1");
                    session.CountersFor("users/1").Increment("likes", 100);
                    session.SaveChanges();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs="
                    }
                };

                var backupTaskId = (store.Maintenance.Send(new UpdatePeriodicBackupOperation(config))).TaskId;
                store.Maintenance.Send(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var status = store.Maintenance.Send(operation).Status;
                    return status?.LastEtag;
                }, 4);
                Assert.Equal(4, value);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                var key = new byte[(int)Sodium.crypto_secretstream_xchacha20poly1305_keybytes()];
                fixed (byte* pKey = key)
                {
                    Sodium.crypto_secretstream_xchacha20poly1305_keygen(pKey);
                }

                var e = Assert.Throws<RavenException>(() =>
                {
                    using (RestoreDatabase(store, new RestoreBackupConfiguration
                    {
                        BackupLocation = Directory.GetDirectories(backupPath).First(),
                        DatabaseName = databaseName,
                        BackupEncryptionSettings = new BackupEncryptionSettings
                        {
                            Key = Convert.ToBase64String(key)
                        }
                    }))
                    {
                    }
                });
                Assert.IsType<CryptographicException>(e.InnerException);
            }
        }

        [Fact]
        public async Task snapshot_encrypted_db_with_new_key_fail()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Snapshot,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;

                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    var errorException = getPeriodicBackupResult.Status?.Error.Exception.Contains("InvalidOperationException");
                    return errorException;
                }, true);
            }
        }

        [Fact]
        public async Task snapshot_unencrypted_db_and_encrypted_backup_fail_1()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Snapshot,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        Key = "OI7Vll7DroXdUORtc6Uo64wdAk1W0Db9ExXXgcg5IUs=",
                        EncryptionMode = EncryptionMode.UseProvidedKey
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    var errorException = getPeriodicBackupResult.Status?.Error.Exception.Contains("InvalidOperationException");
                    return errorException;
                }, true);
            }
        }

        [Fact]
        public async Task snapshot_unencrypted_db_and_encrypted_backup_fail_2()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Snapshot,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.UseDatabaseKey
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;
                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    var errorException = getPeriodicBackupResult.Status?.Error.Exception.Contains("InvalidOperationException");
                    return errorException;
                }, true);
            }
        }

        [Fact]
        public async Task snapshot_and_restore_unencrypted_db_and_unencrypted_backup_1()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");

            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Snapshot,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;

                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Fact]
        public async Task snapshot_and_restore_unencrypted_db_and_unencrypted_backup_2()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");

            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "oren"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Snapshot,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.None
                    }
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;

                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = Directory.GetDirectories(backupPath).First(),
                    DatabaseName = databaseName
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var users = session.Load<User>("users/1");
                        Assert.NotNull(users);
                    }
                }
            }
        }

        [Theory]
        [InlineData(BackupType.Backup, Constants.Documents.PeriodicBackup.EncryptedFullBackupExtension)]
        [InlineData(BackupType.Snapshot, Constants.Documents.PeriodicBackup.EncryptedSnapshotExtension)]
        public async Task backup_encrypted_db_without_backup_encryption_configuration(BackupType backupType, string expectedExtension)
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            var key = EncryptedServer(out var certificates, out string dbName);

            using (var store = GetDocumentStore(new Options
            {
                AdminCertificate = certificates.ServerCertificate.Value,
                ClientCertificate = certificates.ServerCertificate.Value,
                ModifyDatabaseName = s => dbName,
                ModifyDatabaseRecord = record => record.Encrypted = true,
                Path = NewDataPath()
            }))
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User
                    {
                        Name = "Grisha"
                    }, "users/1");
                    await session.SaveChangesAsync();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = backupType,
                    LocalSettings = new LocalSettings
                    {
                        FolderPath = backupPath
                    },
                    IncrementalBackupFrequency = "0 */6 * * *",
                };

                var backupTaskId = (await store.Maintenance.SendAsync(new UpdatePeriodicBackupOperation(config))).TaskId;

                await store.Maintenance.SendAsync(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                var value = WaitForValue(() =>
                {
                    var getPeriodicBackupResult = store.Maintenance.Send(operation);
                    return getPeriodicBackupResult.Status?.LastEtag;
                }, 1);
                Assert.Equal(1, value);

                var folderPath = Directory.GetDirectories(backupPath).First();
                var filePath = Directory.GetFiles(folderPath).First();
                var extension = Path.GetExtension(filePath);

                Assert.Equal(expectedExtension, extension);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                using (RestoreDatabase(store, new RestoreBackupConfiguration
                {
                    BackupLocation = folderPath,
                    DatabaseName = databaseName,
                    EncryptionKey = key,
                    BackupEncryptionSettings = new BackupEncryptionSettings
                    {
                        EncryptionMode = EncryptionMode.UseDatabaseKey,
                        Key = key
                    }
                }))
                {
                    using (var session = store.OpenSession(databaseName))
                    {
                        var user = session.Load<User>("users/1");
                        Assert.NotNull(user);
                    }
                }
            }
        }
    }
}
