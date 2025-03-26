using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Example.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "OrdersBySlot",
                schema: "public",
                columns: table => new
                {
                    TxHash = table.Column<string>(type: "text", nullable: false),
                    Index = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    OwnerAddress = table.Column<string>(type: "text", nullable: false),
                    PolicyId = table.Column<string>(type: "text", nullable: false),
                    AssetName = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SpentSlot = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    BuyerAddress = table.Column<string>(type: "text", nullable: true),
                    SpentTxHash = table.Column<string>(type: "text", nullable: true),
                    RawData = table.Column<byte[]>(type: "bytea", nullable: false),
                    DatumRaw = table.Column<byte[]>(type: "bytea", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdersBySlot", x => new { x.Slot, x.TxHash, x.Index });
                });

            migrationBuilder.CreateTable(
                name: "ReducerStates",
                schema: "public",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReducerStates", x => new { x.Name, x.Slot });
                });

            migrationBuilder.CreateTable(
                name: "TxOutputsBySlot",
                schema: "public",
                columns: table => new
                {
                    TxHash = table.Column<string>(type: "text", nullable: false),
                    Index = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    OwnerAddress = table.Column<string>(type: "text", nullable: false),
                    SpentSlot = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RawData = table.Column<byte[]>(type: "bytea", nullable: false),
                    DatumRaw = table.Column<byte[]>(type: "bytea", nullable: true),
                    AmountRaw = table.Column<byte[]>(type: "bytea", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TxOutputsBySlot", x => new { x.Slot, x.TxHash, x.Index });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReducerStates_Name_Slot",
                schema: "public",
                table: "ReducerStates",
                columns: new[] { "Name", "Slot" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ReducerStates_Slot",
                schema: "public",
                table: "ReducerStates",
                column: "Slot",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdersBySlot",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ReducerStates",
                schema: "public");

            migrationBuilder.DropTable(
                name: "TxOutputsBySlot",
                schema: "public");
        }
    }
}
