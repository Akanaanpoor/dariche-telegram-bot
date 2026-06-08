using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dariche.Commerce.Migrations
{
    /// <inheritdoc />
    public partial class FixOrderPlanRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Plans_PlanId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Plans_PlanId1",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PlanId1",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PlanId1",
                table: "Orders");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Plans_PlanId",
                table: "Orders",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Plans_PlanId",
                table: "Orders");

            migrationBuilder.AddColumn<Guid>(
                name: "PlanId1",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PlanId1",
                table: "Orders",
                column: "PlanId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Plans_PlanId",
                table: "Orders",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Plans_PlanId1",
                table: "Orders",
                column: "PlanId1",
                principalTable: "Plans",
                principalColumn: "Id");
        }
    }
}
