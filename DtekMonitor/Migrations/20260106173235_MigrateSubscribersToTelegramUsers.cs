using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DtekMonitor.Migrations
{
    /// <inheritdoc />
    public partial class MigrateSubscribersToTelegramUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate existing subscribers to telegram.Users table
            // Uses ON CONFLICT to be idempotent (safe to run multiple times)
            migrationBuilder.Sql(@"
                INSERT INTO telegram.""Users"" (
                    ""TelegramId"",
                    ""Username"",
                    ""FirstName"",
                    ""LastName"",
                    ""LanguageCode"",
                    ""IsPremium"",
                    ""IsBlocked"",
                    ""IsActive"",
                    ""BlockedAt"",
                    ""CreatedAt"",
                    ""UpdatedAt"",
                    ""LastActivityAt""
                )
                SELECT 
                    s.""ChatId"",           -- TelegramId = ChatId
                    s.""Username"",         -- Username (may be null)
                    NULL,                   -- FirstName (unknown)
                    NULL,                   -- LastName (unknown)
                    'uk',                   -- LanguageCode (assume Ukrainian)
                    false,                  -- IsPremium
                    false,                  -- IsBlocked
                    true,                   -- IsActive
                    NULL,                   -- BlockedAt
                    s.""CreatedAt"",        -- CreatedAt
                    s.""UpdatedAt"",        -- UpdatedAt
                    s.""UpdatedAt""         -- LastActivityAt (use UpdatedAt as best guess)
                FROM ""Subscribers"" s
                WHERE NOT EXISTS (
                    SELECT 1 FROM telegram.""Users"" u 
                    WHERE u.""TelegramId"" = s.""ChatId""
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Note: Down migration does NOT delete users to prevent data loss
            // If you need to rollback, manually handle the data
            
            // Optionally, you could delete only users that came from Subscribers:
            // migrationBuilder.Sql(@"
            //     DELETE FROM telegram.""Users"" u
            //     WHERE EXISTS (
            //         SELECT 1 FROM ""Subscribers"" s 
            //         WHERE s.""ChatId"" = u.""TelegramId""
            //     );
            // ");
        }
    }
}
