using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dariche.Commerce.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    AgentId = table.Column<string>(type: "text", nullable: false),
                    Secret = table.Column<string>(type: "text", nullable: false),
                    Hostname = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<string>(type: "text", nullable: true),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.AgentId);
                });

            migrationBuilder.CreateTable(
                name: "InboundGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TrafficGb = table.Column<int>(type: "integer", nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    PriceToman = table.Column<decimal>(type: "numeric", nullable: false),
                    PriceStars = table.Column<int>(type: "integer", nullable: true),
                    InboundGroupCode = table.Column<string>(type: "text", nullable: false),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<string>(type: "text", nullable: false),
                    ClientEmail = table.Column<string>(type: "text", nullable: false),
                    ClientUuid = table.Column<string>(type: "text", nullable: false),
                    SubId = table.Column<string>(type: "text", nullable: false),
                    SubscriptionUrl = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TrafficGb = table.Column<int>(type: "integer", nullable: false),
                    ConsumedBytes = table.Column<long>(type: "bigint", nullable: false),
                    AssignedInboundTags = table.Column<string>(type: "text", nullable: false),
                    ExpireAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramUsers", x => x.Id);
                    table.UniqueConstraint("AK_TelegramUsers_TelegramId", x => x.TelegramId);
                });

            migrationBuilder.CreateTable(
                name: "InboundGroupItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InboundGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    InboundTag = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundGroupItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundGroupItems_InboundGroups_InboundGroupId",
                        column: x => x.InboundGroupId,
                        principalTable: "InboundGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmountToman = table.Column<decimal>(type: "numeric", nullable: false),
                    FinalAmountToman = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UserReceiptText = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PlanId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Orders_Plans_PlanId1",
                        column: x => x.PlanId1,
                        principalTable: "Plans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Orders_TelegramUsers_TelegramUserId",
                        column: x => x.TelegramUserId,
                        principalTable: "TelegramUsers",
                        principalColumn: "TelegramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProvisioningJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAgentId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    UserNotified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PickedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisioningJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProvisioningJobs_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboundGroupItems_InboundGroupId",
                table: "InboundGroupItems",
                column: "InboundGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundGroups_Code",
                table: "InboundGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PlanId",
                table: "Orders",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PlanId1",
                table: "Orders",
                column: "PlanId1");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TelegramUserId",
                table: "Orders",
                column: "TelegramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Code",
                table: "Plans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningJobs_OrderId",
                table: "ProvisioningJobs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningJobs_TargetAgentId_Status_CreatedAtUtc",
                table: "ProvisioningJobs",
                columns: new[] { "TargetAgentId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Key",
                table: "Settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_ClientEmail",
                table: "Subscriptions",
                column: "ClientEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramUsers_TelegramId",
                table: "TelegramUsers",
                column: "TelegramId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "InboundGroupItems");

            migrationBuilder.DropTable(
                name: "ProvisioningJobs");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "InboundGroups");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropTable(
                name: "TelegramUsers");
        }
    }
}
