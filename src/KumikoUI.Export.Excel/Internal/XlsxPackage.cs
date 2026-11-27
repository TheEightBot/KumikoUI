namespace KumikoUI.Export.Excel.Internal;

/// <summary>The small fixed parts of the package around the sheet: content types, relationships, workbook, properties.</summary>
internal static class XlsxPackage
{
    private const string Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n";

    public static void ContentTypes(XmlText x, bool coreProperties)
    {
        x.Raw(Declaration);
        x.Raw("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        x.Raw("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        x.Raw("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        x.Raw("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        x.Raw("<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        x.Raw("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        if (coreProperties)
            x.Raw("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
        x.Raw("</Types>");
    }

    public static void PackageRelationships(XmlText x, bool coreProperties)
    {
        x.Raw(Declaration);
        x.Raw("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        x.Raw("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>");
        if (coreProperties)
            x.Raw("<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>");
        x.Raw("</Relationships>");
    }

    public static void CoreProperties(XmlText x, string? title, string? author)
    {
        x.Raw(Declaration);
        x.Raw("<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" ")
            .Raw("xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" ")
            .Raw("xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
        if (title != null) x.Raw("<dc:title>").Text(title).Raw("</dc:title>");
        if (author != null) x.Raw("<dc:creator>").Text(author).Raw("</dc:creator>");
        x.Raw("</cp:coreProperties>");
    }

    public static void Workbook(XmlText x, XlsxLayout layout)
    {
        x.Raw(Declaration);
        x.Raw("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ")
            .Raw("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        x.Raw("<bookViews><workbookView/></bookViews>");
        x.Raw("<sheets><sheet name=\"").Attribute(layout.SheetName).Raw("\" sheetId=\"1\" r:id=\"rId1\"/></sheets>");
        if (layout.AutoFilterName != null)
        {
            // Excel keeps an autofilter's range in this hidden name and writes it for every filtered sheet.
            x.Raw("<definedNames><definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"0\" hidden=\"1\">")
                .Text(layout.AutoFilterName).Raw("</definedName></definedNames>");
        }

        x.Raw("</workbook>");
    }

    public static void WorkbookRelationships(XmlText x)
    {
        x.Raw(Declaration);
        x.Raw("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        x.Raw("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>");
        x.Raw("<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        x.Raw("</Relationships>");
    }
}
