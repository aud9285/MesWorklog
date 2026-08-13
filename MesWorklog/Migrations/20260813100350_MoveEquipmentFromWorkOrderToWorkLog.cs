using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesWorklog.Migrations
{
    /// <inheritdoc />
    public partial class MoveEquipmentFromWorkOrderToWorkLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_work_orders_equipments_equipment_id",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "ix_work_orders_equipment_id",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "equipment_id",
                table: "work_orders");

            migrationBuilder.AddColumn<int>(
                name: "equipment_id",
                table: "work_logs",
                type: "int",
                nullable: true,
                comment: "설비 ID(수작업이면 NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_work_logs_equipment_id",
                table: "work_logs",
                column: "equipment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_work_logs_equipments_equipment_id",
                table: "work_logs",
                column: "equipment_id",
                principalTable: "equipments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_work_logs_equipments_equipment_id",
                table: "work_logs");

            migrationBuilder.DropIndex(
                name: "ix_work_logs_equipment_id",
                table: "work_logs");

            migrationBuilder.DropColumn(
                name: "equipment_id",
                table: "work_logs");

            migrationBuilder.AddColumn<int>(
                name: "equipment_id",
                table: "work_orders",
                type: "int",
                nullable: true,
                comment: "설비 ID(수작업이면 NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_equipment_id",
                table: "work_orders",
                column: "equipment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_work_orders_equipments_equipment_id",
                table: "work_orders",
                column: "equipment_id",
                principalTable: "equipments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
