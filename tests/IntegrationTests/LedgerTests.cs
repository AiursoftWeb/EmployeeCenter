namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class LedgerTests : TestBase
{
    [TestMethod]
    public async Task TestLedgerFlow()
    {
        // 1. Setup - Create an entity
        int entityId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = new CompanyEntity
            {
                CompanyName = "Test Entity",
                EntityCode = "TE001",
                BaseCurrency = "HKD",
                CreateLedger = true
            };
            db.CompanyEntities.Add(entity);
            await db.SaveChangesAsync();
            entityId = entity.Id;
        }

        // 2. Login as Admin
        await LoginAsAdmin();

        // 3. Create Accounts
        var createAccountResponse = await PostForm("/Ledger/CreateAccount", new Dictionary<string, string>
        {
            { "EntityId", entityId.ToString() },
            { "AccountName", "Bank" },
            { "AccountType", FinanceAccountType.Asset.ToString() },
            { "Currency", "HKD" }
        });
        AssertRedirect(createAccountResponse, "/Ledger/Accounts/" + entityId);

        var createAccountResponse2 = await PostForm("/Ledger/CreateAccount", new Dictionary<string, string>
        {
            { "EntityId", entityId.ToString() },
            { "AccountName", "Capital" },
            { "AccountType", FinanceAccountType.Equity.ToString() },
            { "Currency", "HKD" }
        });
        AssertRedirect(createAccountResponse2, "/Ledger/Accounts/" + entityId);

        int bankId, capitalId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            bankId = db.FinanceAccounts.First(a => a.AccountName == "Bank").Id;
            capitalId = db.FinanceAccounts.First(a => a.AccountName == "Capital").Id;
        }

        // 4. Create Transaction (Injection)
        var createTransactionResponse = await PostForm("/Ledger/CreateTransaction", new Dictionary<string, string>
        {
            { "EntityId", entityId.ToString() },
            { "Description", "Initial Capital" },
            { "SourceAccountId", capitalId.ToString() },
            { "DestinationAccountId", bankId.ToString() },
            { "Amount", "10000" },
            { "ExchangeRate", "1" },
            { "TransactionTime", DateTime.UtcNow.ToString("O") }
        });
        AssertRedirect(createTransactionResponse, "/Ledger/Dashboard/" + entityId);

        // 5. Verify Balance
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();

            // Asset: Destination - Source = 10000 - 0 = 10000
            var bankBalance = await GetBalance(db, bankId);
            Assert.AreEqual(10000, bankBalance);

            // Equity: Source - Destination = 10000 - 0 = 10000
            var capitalBalance = await GetBalance(db, capitalId);
            Assert.AreEqual(10000, capitalBalance);
        }

        // 6. Create Expense Transaction
        int expenseAccountId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var expenseAccount = new FinanceAccount
            {
                AccountName = "Legal Fee",
                AccountType = FinanceAccountType.Expense,
                CompanyEntityId = entityId,
                Currency = "HKD"
            };
            db.FinanceAccounts.Add(expenseAccount);
            await db.SaveChangesAsync();
            expenseAccountId = expenseAccount.Id;
        }

        var createExpenseResponse = await PostForm("/Ledger/CreateTransaction", new Dictionary<string, string>
        {
            { "EntityId", entityId.ToString() },
            { "Description", "Sleek fee" },
            { "SourceAccountId", bankId.ToString() },
            { "DestinationAccountId", expenseAccountId.ToString() },
            { "Amount", "3000" },
            { "ExchangeRate", "1" },
            { "TransactionTime", DateTime.UtcNow.ToString("O") }
        });
        AssertRedirect(createExpenseResponse, "/Ledger/Dashboard/" + entityId);

        // 7. Verify Final Balances
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();

            // Bank: 10000 - 3000 = 7000
            Assert.AreEqual(7000, await GetBalance(db, bankId));

            // Expense: 3000 - 0 = 3000
            Assert.AreEqual(3000, await GetBalance(db, expenseAccountId));
        }

        // 8. Edit Transaction
        int transactionId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            transactionId = db.Transactions.First(t => t.Description == "Sleek fee").Id;
        }

        var editTransactionResponse = await PostForm("/Ledger/EditTransaction", new Dictionary<string, string>
        {
            { "EntityId", entityId.ToString() },
            { "TransactionId", transactionId.ToString() },
            { "Description", "Sleek fee updated" },
            { "SourceAccountId", bankId.ToString() },
            { "DestinationAccountId", expenseAccountId.ToString() },
            { "Amount", "4000" },
            { "ExchangeRate", "1" },
            { "TransactionTime", DateTime.UtcNow.AddDays(-1).ToString("O") }
        });
        AssertRedirect(editTransactionResponse, "/Ledger/Dashboard/" + entityId);

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var transaction = await db.Transactions.FindAsync(transactionId);
            Assert.AreEqual("Sleek fee updated", transaction!.Description);
            Assert.AreEqual(4000, transaction.Amount);

            // Bank: 10000 - 4000 = 6000
            Assert.AreEqual(6000, await GetBalance(db, bankId));
        }

        // 9. Delete Transaction
        var deleteTransactionResponse = await PostForm("/Ledger/DeleteTransaction", new Dictionary<string, string>
        {
            { "id", transactionId.ToString() },
            { "entityId", entityId.ToString() }
        });
        AssertRedirect(deleteTransactionResponse, "/Ledger/Dashboard/" + entityId);

        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var transaction = await db.Transactions.FindAsync(transactionId);
            Assert.IsNull(transaction);

            // Bank: 10000 - 0 = 10000 (since only one transaction remains)
            Assert.AreEqual(10000, await GetBalance(db, bankId));
        }
    }

    [TestMethod]
    public async Task TestTransactionMaterials()
    {
        // 1. Setup - Create an entity and accounts
        int entityId;
        int bankId, capitalId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = new CompanyEntity
            {
                CompanyName = "Materials Test Entity",
                EntityCode = "MTE01",
                BaseCurrency = "USD",
                CreateLedger = true
            };
            db.CompanyEntities.Add(entity);
            await db.SaveChangesAsync();
            entityId = entity.Id;

            var bank = new FinanceAccount { AccountName = "Bank", AccountType = FinanceAccountType.Asset, CompanyEntityId = entityId, Currency = "USD", ShowInDashboard = true };
            var capital = new FinanceAccount { AccountName = "Capital", AccountType = FinanceAccountType.Equity, CompanyEntityId = entityId, Currency = "USD", ShowInDashboard = true };
            db.FinanceAccounts.AddRange(bank, capital);
            await db.SaveChangesAsync();
            bankId = bank.Id;
            capitalId = capital.Id;
        }

        // 2. Login
        await LoginAsAdmin();

        // 3. Create Transaction with Materials
        var createTransactionResponse = await PostForm("/Ledger/CreateTransaction", new Dictionary<string, string>
        {
            { "EntityId", entityId.ToString() },
            { "Description", "Transaction with materials" },
            { "SourceAccountId", capitalId.ToString() },
            { "DestinationAccountId", bankId.ToString() },
            { "Amount", "100" },
            { "ExchangeRate", "1" },
            { "InvoicePath", "vault/invoices/test.pdf" },
            { "MT103Path", "vault/mt103/test.pdf" },
            { "PaymentVoucherPath", "vault/vouchers/test.pdf" },
            { "TransactionTime", DateTime.UtcNow.ToString("O") }
        });
        AssertRedirect(createTransactionResponse, "/Ledger/Dashboard/" + entityId);

        // 4. Verify in DB
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.Description == "Transaction with materials");
            Assert.IsNotNull(transaction);
            Assert.AreEqual("vault/invoices/test.pdf", transaction.InvoicePath);
            Assert.AreEqual("vault/mt103/test.pdf", transaction.MT103Path);
            Assert.AreEqual("vault/vouchers/test.pdf", transaction.PaymentVoucherPath);
        }

        // 5. Verify in Dashboard View
        var dashboardResponse = await Http.GetAsync($"/Ledger/Dashboard/{entityId}");
        var dashboardContent = await dashboardResponse.Content.ReadAsStringAsync();
        Assert.Contains("fa-file-invoice", dashboardContent);
        Assert.Contains("fa-file-contract", dashboardContent);
        Assert.Contains("fa-receipt", dashboardContent);

        // 6. Verify in Transactions View
        var transactionsResponse = await Http.GetAsync($"/Ledger/Transactions/{entityId}");
        var transactionsContent = await transactionsResponse.Content.ReadAsStringAsync();
        Assert.Contains("fa-file-invoice", transactionsContent);
        Assert.Contains("fa-file-contract", transactionsContent);
        Assert.Contains("fa-receipt", transactionsContent);
    }

    [TestMethod]
    public async Task TestAccountEditAndDashboardFiltering()
    {
        // 1. Setup - Create an entity and an account
        int entityId;
        int accountId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = new CompanyEntity
            {
                CompanyName = "Filter Test Entity",
                EntityCode = "FTE01",
                BaseCurrency = "USD",
                CreateLedger = true
            };
            db.CompanyEntities.Add(entity);
            await db.SaveChangesAsync();
            entityId = entity.Id;

            var account = new FinanceAccount
            {
                AccountName = "Initial Name",
                AccountType = FinanceAccountType.Asset,
                CompanyEntityId = entityId,
                Currency = "USD",
                ShowInDashboard = true
            };
            db.FinanceAccounts.Add(account);
            await db.SaveChangesAsync();
            accountId = account.Id;
        }

        // 2. Login
        await LoginAsAdmin();

        // 3. Edit Account - Rename and Hide from Dashboard
        var editAccountResponse = await PostForm("/Ledger/EditAccount", new Dictionary<string, string>
        {
            { "Id", accountId.ToString() },
            { "EntityId", entityId.ToString() },
            { "AccountName", "Updated Name" },
            { "AccountType", FinanceAccountType.Liability.ToString() },
            { "Currency", "EUR" },
            { "ShowInDashboard", "false" },
            { "IsArchived", "false" }
        });
        AssertRedirect(editAccountResponse, "/Ledger/Accounts/" + entityId);

        // 4. Verify in DB
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var account = await db.FinanceAccounts.FindAsync(accountId);
            Assert.IsNotNull(account);
            Assert.AreEqual("Updated Name", account.AccountName);
            Assert.AreEqual(FinanceAccountType.Liability, account.AccountType);
            Assert.AreEqual("EUR", account.Currency);
            Assert.IsFalse(account.ShowInDashboard);
        }

        // 5. Verify Dashboard page loads successfully
        var dashboardResponse = await Http.GetAsync("/Ledger/Dashboard/" + entityId);
        dashboardResponse.EnsureSuccessStatusCode();

        // 5b. Verify DashboardSummaryApi does not include the hidden account
        var summaryResponse = await Http.GetAsync($"/Ledger/DashboardSummaryApi?id={entityId}");
        var summaryContent = await summaryResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Updated Name", summaryContent, "Hidden account should not be in dashboard summary API.");

        // 6. Show it again
        await PostForm("/Ledger/EditAccount", new Dictionary<string, string>
        {
            { "Id", accountId.ToString() },
            { "EntityId", entityId.ToString() },
            { "AccountName", "Updated Name" },
            { "AccountType", FinanceAccountType.Liability.ToString() },
            { "Currency", "EUR" },
            { "ShowInDashboard", "true" },
            { "IsArchived", "false" }
        });

        // 7. Verify DashboardSummaryApi now includes the account
        var summaryResponse2 = await Http.GetAsync($"/Ledger/DashboardSummaryApi?id={entityId}");
        var summaryContent2 = await summaryResponse2.Content.ReadAsStringAsync();
        Assert.Contains("Updated Name", summaryContent2, "Visible account should be in dashboard summary API.");
    }

    [TestMethod]
    public async Task TestDashboardFilteredView()
    {
        // 1. Setup - Create an entity and two accounts
        int entityId;
        int accountAId, accountBId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = new CompanyEntity
            {
                CompanyName = "Filtered Dashboard Entity",
                EntityCode = "FDE01",
                BaseCurrency = "USD",
                CreateLedger = true
            };
            db.CompanyEntities.Add(entity);
            await db.SaveChangesAsync();
            entityId = entity.Id;

            var accountA = new FinanceAccount { AccountName = "Account A", AccountType = FinanceAccountType.Asset, CompanyEntityId = entityId, Currency = "USD", ShowInDashboard = true };
            var accountB = new FinanceAccount { AccountName = "Account B", AccountType = FinanceAccountType.Asset, CompanyEntityId = entityId, Currency = "USD", ShowInDashboard = true };
            db.FinanceAccounts.AddRange(accountA, accountB);
            await db.SaveChangesAsync();
            accountAId = accountA.Id;
            accountBId = accountB.Id;

            // Transaction for Account A
            db.Transactions.Add(new Transaction { Description = "Transaction A", SourceAccountId = accountBId, DestinationAccountId = accountAId, Amount = 100, ExchangeRate = 1, TransactionTime = DateTime.UtcNow });
            // Transaction for Account B only (not A)
            db.Transactions.Add(new Transaction { Description = "Transaction B Only", SourceAccountId = accountBId, DestinationAccountId = accountBId, Amount = 50, ExchangeRate = 1, TransactionTime = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        // 2. Login
        await LoginAsAdmin();

        // 3. View Main Dashboard (page loads successfully)
        var dashboardResponse = await Http.GetAsync($"/Ledger/Dashboard/{entityId}");
        dashboardResponse.EnsureSuccessStatusCode();

        // 3b. Verify Summary API contains both accounts
        var summaryResponse = await Http.GetAsync($"/Ledger/DashboardSummaryApi?id={entityId}");
        var summaryContent = await summaryResponse.Content.ReadAsStringAsync();
        Assert.Contains("Account A", summaryContent);
        Assert.Contains("Account B", summaryContent);

        // 3c. Verify Transactions API contains Transaction A
        var txResponse = await Http.GetAsync($"/Ledger/DashboardTransactionsApi?id={entityId}");
        var txContent = await txResponse.Content.ReadAsStringAsync();
        Assert.Contains("Transaction A", txContent);

        // 4. View Filtered Dashboard for Account A
        var filteredResponse = await Http.GetAsync($"/Ledger/Dashboard/{entityId}?accountId={accountAId}");
        var filteredContent = await filteredResponse.Content.ReadAsStringAsync();

        // Should contain account name in the filtered view header
        Assert.Contains("Account A - Dashboard", filteredContent);

        // 4b. Verify Transactions API for filtered account contains Transaction A
        var filteredTxResponse = await Http.GetAsync($"/Ledger/DashboardTransactionsApi?id={entityId}&accountId={accountAId}");
        var filteredTxContent = await filteredTxResponse.Content.ReadAsStringAsync();
        Assert.Contains("Transaction A", filteredTxContent);

        // Should NOT contain Transaction B Only (it doesn't involve Account A)
        Assert.DoesNotContain("Transaction B Only", filteredTxContent);
    }

    [TestMethod]
    public async Task TestLedgerIndexFiltering()
    {
        // 1. Setup - Create two entities, one with CreateLedger = true, one with false
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            db.CompanyEntities.AddRange(
                new CompanyEntity
                {
                    CompanyName = "Show In Ledger",
                    EntityCode = "SIL01",
                    CreateLedger = true
                },
                new CompanyEntity
                {
                    CompanyName = "Hide From Ledger",
                    EntityCode = "HFL01",
                    CreateLedger = false
                }
            );
            await db.SaveChangesAsync();
        }

        // 2. Login as Admin
        await LoginAsAdmin();

        // 3. View Ledger Index
        var response = await Http.GetAsync("/Ledger/Index");
        var content = await response.Content.ReadAsStringAsync();

        // 4. Verify
        Assert.Contains("Show In Ledger", content);
        Assert.DoesNotContain("Hide From Ledger", content);
    }

    private async Task<decimal> GetBalance(EmployeeCenterDbContext db, int accountId)
    {
        var account = await db.FinanceAccounts.FindAsync(accountId);
        var sourceSum = await db.Transactions
            .Where(t => t.SourceAccountId == accountId)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        var destinationSum = await db.Transactions
            .Where(t => t.DestinationAccountId == accountId)
            .SumAsync(t => (decimal?)t.Amount * t.ExchangeRate) ?? 0;

        return account!.AccountType switch
        {
            FinanceAccountType.Asset or FinanceAccountType.Expense => destinationSum - sourceSum,
            FinanceAccountType.Liability or FinanceAccountType.Equity or FinanceAccountType.Income => sourceSum - destinationSum,
            _ => 0
        };
    }

    /// <summary>
    /// Regression test: DashboardAssetTrendApi must not throw 500 when the underlying query
    /// mixes OR navigation conditions (SourceAccount || DestinationAccount) with DateTime
    /// comparisons and Asset/Liability account types in the same Where chain.
    ///
    /// Before the fix, Pomelo MySQL EF Core provider's SqlNullabilityProcessor would crash
    /// with "Unhandled expression MySqlComplexFunctionArgumentExpression" because it
    /// doesn't know how to handle this specific expression tree combination.
    ///
    /// The fix separates the entityId OR and the DateTime range into two distinct
    /// Where() calls, and removes the redundant AccountType OR conditions from the server-side
    /// query (they are already filtered in-memory in Phase 3 of GetMonthlyAssetTrendAsync).
    /// </summary>
    [TestMethod]
    public async Task DashboardAssetTrendApi_WithAssetAndLiabilityTransactions_ReturnsValidTrendData()
    {
        // 1. Setup: Create entity with CreateLedger = true and Asset/Liability accounts
        int entityId;
        int assetAccountId, liabilityAccountId, equityAccountId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var entity = new CompanyEntity
            {
                CompanyName = "AssetTrend Test Entity",
                EntityCode = "ATT01",
                BaseCurrency = "USD",
                CreateLedger = true
            };
            db.CompanyEntities.Add(entity);
            await db.SaveChangesAsync();
            entityId = entity.Id;

            // Asset account (source of truth for initial balance)
            var assetAccount = new FinanceAccount
            {
                CompanyEntityId = entityId,
                AccountName = "Cash",
                AccountType = FinanceAccountType.Asset,
                Currency = "USD",
                ShowInDashboard = true
            };
            // Liability account
            var liabilityAccount = new FinanceAccount
            {
                CompanyEntityId = entityId,
                AccountName = "Loan Payable",
                AccountType = FinanceAccountType.Liability,
                Currency = "USD",
                ShowInDashboard = true
            };
            // Equity account (for initial capital injection)
            var equityAccount = new FinanceAccount
            {
                CompanyEntityId = entityId,
                AccountName = "Retained Earnings",
                AccountType = FinanceAccountType.Equity,
                Currency = "USD",
                ShowInDashboard = true
            };
            db.FinanceAccounts.AddRange(assetAccount, liabilityAccount, equityAccount);
            await db.SaveChangesAsync();

            assetAccountId = assetAccount.Id;
            liabilityAccountId = liabilityAccount.Id;
            equityAccountId = equityAccount.Id;
        }

        // 2. Login as Admin
        await LoginAsAdmin();

        // 3. Create transactions
        //
        // Transaction 1: Equity -> Asset (initial capital injection, Asset account receives funds)
        // Transaction 2: Asset -> Liability (Asset pays Liability, liability owed increases)
        // Transaction 3: Asset -> Liability (Asset pays Liability again)
        //
        // This exercises the exact OR navigation pattern that previously triggered the bug:
        //   t.SourceAccount!.CompanyEntityId == entityId  OR  t.DestinationAccount!.CompanyEntityId == entityId
        // combined with DateTime range and AccountType filtering.
        var utcNow = DateTime.UtcNow;

        var transaction1 = await PostForm("/Ledger/CreateTransaction", new Dictionary<string, string>
        {
            { "EntityId", entityId.ToString() },
            { "Description", "Initial capital injection" },
            { "SourceAccountId", equityAccountId.ToString() },
            { "DestinationAccountId", assetAccountId.ToString() },
            { "Amount", "5000" },
            { "ExchangeRate", "1" },
            { "TransactionTime", utcNow.AddDays(-5).ToString("O") }
        });
        AssertRedirect(transaction1, $"/Ledger/Dashboard/{entityId}");

        var transaction2 = await PostForm("/Ledger/CreateTransaction", new Dictionary<string, string>
        {
            { "EntityId", entityId.ToString() },
            { "Description", "Asset pays liability" },
            { "SourceAccountId", assetAccountId.ToString() },
            { "DestinationAccountId", liabilityAccountId.ToString() },
            { "Amount", "1000" },
            { "ExchangeRate", "1" },
            { "TransactionTime", utcNow.AddDays(-3).ToString("O") }
        });
        AssertRedirect(transaction2, $"/Ledger/Dashboard/{entityId}");

        var transaction3 = await PostForm("/Ledger/CreateTransaction", new Dictionary<string, string>
        {
            { "EntityId", entityId.ToString() },
            { "Description", "Asset pays liability again" },
            { "SourceAccountId", assetAccountId.ToString() },
            { "DestinationAccountId", liabilityAccountId.ToString() },
            { "Amount", "500" },
            { "ExchangeRate", "1" },
            { "TransactionTime", utcNow.AddDays(-1).ToString("O") }
        });
        AssertRedirect(transaction3, $"/Ledger/Dashboard/{entityId}");

        // 4. Call DashboardAssetTrendApi (this previously threw 500 with the buggy query)
        var apiResponse = await Http.GetAsync($"/Ledger/DashboardAssetTrendApi?id={entityId}&year={utcNow.Year}");

        // 5. Verify the response is 200 OK (regression check for the Pomelo MySQL bug)
        Assert.AreEqual(HttpStatusCode.OK, apiResponse.StatusCode,
            "DashboardAssetTrendApi should return 200 OK; a 500 indicates the Pomelo MySQL SqlNullabilityProcessor bug was not fixed.");

        // 6. Verify the response body is a valid AssetTrendResponse
        var apiContent = await apiResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(apiContent, "Labels", "API response should contain Labels field.");
        StringAssert.Contains(apiContent, "AssetData", "API response should contain AssetData field.");
        StringAssert.Contains(apiContent, "LiabilityData", "API response should contain LiabilityData field.");

        // Deserialize and validate the data structure
        var trend = await apiResponse.Content.ReadFromJsonAsync<AssetTrendResponse>();
        Assert.IsNotNull(trend);
        Assert.AreEqual(13, trend.Labels.Length, "Should have 13 labels (Initial + 12 months).");
        Assert.AreEqual(13, trend.AssetData.Length, "Should have 13 asset data points.");
        Assert.AreEqual(13, trend.LiabilityData.Length, "Should have 13 liability data points.");

        // Initial balance should be 0 (no transactions as of Jan 1 of current year)
        Assert.AreEqual(0, trend.AssetData[0], "Initial asset balance should be 0 before capital injection.");
        Assert.AreEqual(0, trend.LiabilityData[0], "Initial liability balance should be 0.");

        // After capital injection (5000 Asset inflow on equity account), asset should increase.
        // The capital injection transaction (equity -> asset) increases asset balance.
        // Find the data point for the current month.
        var currentMonthIndex = utcNow.Month; // 1-based
        Assert.IsTrue(trend.AssetData[currentMonthIndex].HasValue,
            $"Asset data for month {currentMonthIndex} should have a value after transactions.");
        Assert.IsTrue(trend.LiabilityData[currentMonthIndex].HasValue,
            $"Liability data for month {currentMonthIndex} should have a value after transactions.");
    }
}
