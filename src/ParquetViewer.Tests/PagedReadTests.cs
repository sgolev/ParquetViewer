using System.Data;

namespace ParquetViewer.Tests;

/// <summary>
/// Paging reads the file one page at a time through the same engine instance, which means decoded
/// row groups get reused across reads. These tests make sure paging always returns the same data as a full read.
/// </summary>
[TestClass]
public class PagedReadTests
{
    public PagedReadTests()
    {
        //Set a consistent date format for all tests
        ParquetEngineSettings.DateDisplayFormat = "yyyy-MM-dd HH:mm:ss";
        ParquetEngineSettings.DateOnlyDisplayFormat = "yyyy-MM-dd";
        ParquetEngineSettings.TimeOnlyDisplayFormat = "HH:mm:ss";
    }

    [TestMethod]
    [DataRow("Data/LIST_TYPE_TEST1.parquet")] //Two list columns whose leaf fields are both named `item`
    [DataRow("Data/LIST_TYPE_TEST2.parquet")]
    [DataRow("Data/STRUCT_TYPE_TEST.parquet")] //Two map columns whose leaf fields are both named `key`
    [DataRow("Data/NESTED_STRUCTS_AND_LISTS.parquet")] //`ref_reco` appears under three different lists
    [DataRow("Data/LIST_OF_STRUCTS1.parquet")]
    [DataRow("Data/LIST_OF_STRUCTS2.parquet")]
    [DataRow("Data/LIST_OF_STRUCT_OF_LIST_OF_STRUCT.parquet")]
    [DataRow("Data/LIST_OF_LIST_OF_INT.parquet")]
    [DataRow("Data/LIST_OF_LIST_OF_LIST_OF_STRING.parquet")]
    [DataRow("Data/EMPTY_LIST_OF_STRUCTS.parquet")]
    [DataRow("Data/MAP_TYPE_TEST1.parquet")]
    [DataRow("Data/MAP_TYPE_TEST2.parquet")]
    [DataRow("Data/TWO_TIER_TEPEATED_LIST_FIELDS_TEST.parquet")]
    [DataRow("Data/NULLABLE_GUID_TEST.parquet")]
    [DataRow("Data/RANDOM_TEST_FILE.parquet")]
    [DataRow("Data/DATETIME_TEST2.parquet")]
    [DataRow("Data/PARTITIONED_PARQUET_FILE_TEST")] //Pages cross file boundaries
    public async Task PAGED_READS_MATCH_FULL_READ(string path)
    {
        using var parquetEngine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(path);
        //A full read of some fields in NESTED_STRUCTS_AND_LISTS already fails, so only compare the ones a full read can handle
        var fields = path.EndsWith("NESTED_STRUCTS_AND_LISTS.parquet") ? await GetReadableFieldsAsync(parquetEngine) : parquetEngine.Fields;
        Assert.IsNotEmpty(fields);

        var fullRead = (await parquetEngine.ReadRowsAsync(fields, 0, int.MaxValue))(false);
        var expectedRows = ToComparableRows(fullRead);
        Assert.AreEqual(parquetEngine.RecordCount, expectedRows.Count);

        foreach (var pageSize in new[] { 1, 3, 7 })
        {
            //Read forwards
            var pagedRows = new List<string>();
            for (long offset = 0; offset < parquetEngine.RecordCount; offset += pageSize)
            {
                var page = (await parquetEngine.ReadRowsAsync(fields, offset, pageSize))(false);
                pagedRows.AddRange(ToComparableRows(page));
            }
            CollectionAssert.AreEqual(expectedRows, pagedRows, $"Forward paging with page size {pageSize} returned different data");

            //Read backwards, like a user going to previous pages
            var reversePages = new List<List<string>>();
            for (long offset = (parquetEngine.RecordCount - 1) / pageSize * pageSize; offset >= 0; offset -= pageSize)
            {
                var page = (await parquetEngine.ReadRowsAsync(fields, offset, pageSize))(false);
                reversePages.Insert(0, ToComparableRows(page));
            }
            CollectionAssert.AreEqual(expectedRows, reversePages.SelectMany(rows => rows).ToList(), $"Backward paging with page size {pageSize} returned different data");
        }
    }

    [TestMethod]
    public async Task READ_PAST_THE_END_RETURNS_NO_ROWS()
    {
        using var parquetEngine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync("Data/MAP_TYPE_TEST1.parquet");

        var dataTable = (await parquetEngine.ReadRowsAsync(parquetEngine.Fields, parquetEngine.RecordCount + 10, 5))(false);
        Assert.AreEqual(0, dataTable.Rows.Count);
    }

    /// <summary>
    /// The fields a full read can handle. A few list-of-struct fields in NESTED_STRUCTS_AND_LISTS can't be read even then.
    /// </summary>
    private static async Task<List<string>> GetReadableFieldsAsync(IParquetEngine parquetEngine)
    {
        var readableFields = new List<string>();
        foreach (var field in parquetEngine.Fields)
        {
            try
            {
                _ = (await parquetEngine.ReadRowsAsync([field], 0, int.MaxValue))(false);
                readableFields.Add(field);
            }
            catch (ArgumentOutOfRangeException) { /*Known issue with some nested list-of-struct fields*/ }
        }
        return readableFields;
    }

    internal static List<string> ToComparableRows(DataTable dataTable)
        => dataTable.Rows.Cast<DataRow>()
            .Select(row => string.Join(" | ", row.ItemArray.Select(value => value == DBNull.Value ? "<NULL>" : $"{value?.GetType().Name}:{value}")))
            .ToList();
}
