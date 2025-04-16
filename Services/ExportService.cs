using Syncfusion.XlsIO;
using RepPortal.Models;
using RepPortal.Data;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using System.Net;
using System.Net.Mail;
using Microsoft.IdentityModel.Tokens;
using Syncfusion.Blazor.RichTextEditor.Internal;
using Syncfusion.Pdf.ColorSpace;
using Syncfusion.Drawing;
using PointF = Syncfusion.Drawing.PointF;
using RectangleF = Syncfusion.Drawing.RectangleF;
using SizeF = Syncfusion.Drawing.SizeF;
using Color = Syncfusion.Drawing.Color;
using Syncfusion.Pdf.Tables;
using System.Data;
using Syncfusion.Blazor.Kanban.Internal;


namespace RepPortal.Services;

public class ExportService
{
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExportService> _logger;



    public ExportService(IWebHostEnvironment hostingEnvironment, IConfiguration configuration,
        ILogger<ExportService> logger)
    {
        _hostingEnvironment = hostingEnvironment;
        _configuration = configuration;
        _logger = logger;
    }



    public static async Task<byte[]> ExportPcfToExcel(PCFHeader header)
    {
        using ExcelEngine excelEngine = new ExcelEngine();
        IApplication application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        // Create workbook and worksheet
        IWorkbook workbook = application.Workbooks.Create(1);
        IWorksheet worksheet = workbook.Worksheets[0];

        // Write Header Details in the First Section
        worksheet.Range["A1:D1"].Merge();
        worksheet.Range["A1"].Text = $@"PCF {header.PcfNumber} Export";

        worksheet.Range["A1"].CellStyle.Font.Size = 16;
        worksheet.Range["A1"].CellStyle.Font.Bold = true;
        worksheet.Range["A1"].CellStyle.HorizontalAlignment = ExcelHAlign.HAlignCenter;

        worksheet.Range["A2"].Text = "PCF Number:";
        worksheet.Range["B2"].Text = header.PcfNum.ToString();

        worksheet.Range["A3"].Text = "Customer Name (Number):";
        worksheet.Range["B3"].Text = $"{header.CustomerName} ({header.CustomerNumber})";


        worksheet.Range["A4"].Text = "Effective Dates:";
        string dateRange = $"{header.StartDate:MM/dd/yyyy} to {header.EndDate:MM/dd/yyyy}";
        worksheet.Range["B4"].Text = dateRange;


        worksheet.Range["A5"].Text = "Rep Code:";
        worksheet.Range["B5"].Text = header.RepCode;

        worksheet.Range["A6"].Text = "Buying Group:";
        worksheet.Range["B6"].Text = header.BuyingGroup;


        worksheet.Range["A7"].Text = "Bill To Info:";
        worksheet.Range["B7"].Text = $"{header.BillToAddress}, {header.BillToCity}, {header.BTState}, {header.BTZip}";

        worksheet.Range["A8"].Text = "Buyer:";
        worksheet.Range["B8"].Text = header.Buyer;
        worksheet.Range["A9"].Text = "PCF Type:";
        worksheet.Range["B9"].Text = header.PcfType;
        if (!header.PromoPaymentTerms.IsNullOrEmpty())
        {
            worksheet.Range["A10"].Text = "Promo Payment Terms:";
            worksheet.Range["B10"].Text = header.PromoPaymentTerms;
        }

        if (!header.PromoPaymentTermsText.IsNullOrEmpty())
        {
            worksheet.Range["A11"].Text = "Promo Payment Terms Text:";
            worksheet.Range["B11"].Text = header.PromoPaymentTermsText;
        }


        if (!header.FreightTerms.IsNullOrEmpty())
        {
            worksheet.Range["A12"].Text = "Freight Terms:";
            worksheet.Range["B12"].Text = header.FreightTerms;
        }

        if (!header.FreightMinimums.IsNullOrEmpty())
        {
            worksheet.Range["A13"].Text = "Freight Minimums:";
            worksheet.Range["B13"].Text = header.FreightMinimums;
        }

        worksheet.Range["A14"].Text = "General Notes:";
        worksheet.Range["B14"].Text = header.GeneralNotes;

        if (1 == 2)
        {
            worksheet.Range["A15"].Text = "Market Type:";
            worksheet.Range["B15"].Text = header.MarketType;
            worksheet.Range["A2:A15"].CellStyle.Font.Bold = true;
        }

        // Add a separator row
        worksheet.Range["A17:B17"].Merge();
        worksheet.Range["A17"].Text = "PCF Item Details:";
        worksheet.Range["A17"].CellStyle.Font.Bold = true;

        // Write Item Details Starting from Row 19
        var itemHeaders = new[] { "ItemNum", "ItemDesc", "Price", "Family_Code", "Family_Code_Description" };
        for (int i = 0; i < itemHeaders.Length; i++)
        {
            worksheet.Range[18, i + 1].Text = itemHeaders[i];
            worksheet.Range[18, i + 1].CellStyle.Font.Bold = true;
        }

        // Populate PCFItemDTO Data
        int rowIndex = 19; // Start writing item data at row 20
        foreach (var item in header.PCFLines.OrderBy(line => line.ItemNum))
        {
            worksheet.Range[rowIndex, 1].Text = item.ItemNum;
            worksheet.Range[rowIndex, 2].Text = item.ItemDesc;
            worksheet.Range[rowIndex, 3].Number = item.ApprovedPrice;
            worksheet.Range[rowIndex, 4].Text = item.Family_Code;
            worksheet.Range[rowIndex, 5].Text = item.Family_Code_Description;


            rowIndex++;
        }

        worksheet.UsedRange.AutofitColumns();

        // Save workbook to memory stream
        using MemoryStream stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();

    }




    private void FormatCell(IWorksheet sheet, string cellAddress, int fontSize, bool bold = false,
        ExcelHAlign hAlign = ExcelHAlign.HAlignLeft)
    {
        var cell = sheet[cellAddress];
        cell.CellStyle.Font.Size = fontSize;
        cell.CellStyle.Font.Bold = bold;
        cell.CellStyle.HorizontalAlignment = hAlign;
    }

    private void FormatCellText(IWorksheet sheet, string cellAddress, string cellText, int fontSize = 12,
        bool bold = false, ExcelHAlign hAlign = ExcelHAlign.HAlignRight)
    {
        var cell = sheet[cellAddress];
        cell.Text = cellText;
        cell.CellStyle.Font.Size = fontSize;
        cell.CellStyle.Font.Bold = bold;
        cell.CellStyle.HorizontalAlignment = hAlign;
    }

    public MemoryStream CreatePDF()
    {
        PdfDocument document = new PdfDocument();
        PdfPage currentPage = document.Pages.Add();
        Syncfusion.Drawing.SizeF clientSize = currentPage.GetClientSize();
        FileStream imageStream = new FileStream(_hostingEnvironment.WebRootPath + "//images/pdfheader.png",
            FileMode.Open, FileAccess.Read);
        PdfImage banner = new PdfBitmap(imageStream);
        SizeF bannerSize = new SizeF(500, 50);
        PointF bannerLocation = new PointF(0, 0);
        PdfGraphics graphics = currentPage.Graphics;
        graphics.DrawImage(banner, bannerLocation, bannerSize);
        // PdfFont font = new PdfStandardFont(PdfFontFamily.Helvetica, 20, PdfFontStyle.Bold);
        //var headerText = new PdfTextElement("INVOICE", font, new PdfSolidBrush(Color.FromArgb(1, 53, 67, 168)));
        // headerText.StringFormat = new PdfStringFormat(PdfTextAlignment.Right);
        // PdfLayoutResult result = headerText.Draw(currentPage, new PointF(clientSize.Width - 25, iconLocation.Y + 10));

        MemoryStream stream = new MemoryStream();
        document.Save(stream);
        document.Close(true);
        stream.Position = 0;

        return stream;
    }

    public MemoryStream CreatePCFPDF(PCFHeader header)
    {
        ;
        PdfDocument document = new PdfDocument();
        PdfPage page = document.Pages.Add();
        PdfGraphics graphics = page.Graphics;
        SizeF clientSize = page.GetClientSize();

        // Draw banner image
        using (FileStream imageStream = new FileStream(_hostingEnvironment.WebRootPath + "//images/pdfheader.png",
                   FileMode.Open, FileAccess.Read))
        {
            PdfImage banner = new PdfBitmap(imageStream);
            SizeF bannerSize = new SizeF(500, 50);
            PointF bannerLocation = new PointF(0, 0);
            graphics.DrawImage(banner, bannerLocation, bannerSize);
        }

        // Define fonts
        PdfFont titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);
        PdfFont headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
        PdfFont regularFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
        PdfFont infoFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
        PdfFont smallFont = new PdfStandardFont(PdfFontFamily.Helvetica, 8);

        // Define margins
        float marginX = 40;
        float yPosition = 70; // Start a little lower after the banner

        // Draw document title
        graphics.DrawString("Contracted Pricing Information", titleFont, PdfBrushes.Black,
            new PointF(marginX, yPosition));
        yPosition += 25;



        // Draw customer information
        graphics.DrawString($"Customer: [{header.CustomerNumber}] {header.CustomerName}", headerFont, PdfBrushes.Black,
            new PointF(marginX, yPosition));
        yPosition += 20;

        // Draw PCF info


        // Draw effective date range
        string effectiveDateString =
            $"Approved prices for dates {header.StartDate:MM/dd/yyyy} through {header.EndDate:MM/dd/yyyy}";
        graphics.DrawString(effectiveDateString, infoFont, PdfBrushes.Black, new PointF(marginX, yPosition));
        yPosition += 15;

        if (!header.PromoPaymentTerms.IsNullOrEmpty())
        {
            string paymentTerms = header.PromoPaymentTermsText;
            graphics.DrawString($"Payment Terms: {paymentTerms}", infoFont, PdfBrushes.Black,
                new PointF(marginX, yPosition));
            yPosition += 15;
        }

        if (!header.FreightTerms.IsNullOrEmpty())
        {
            string freightTerms = header.FreightTerms;
            graphics.DrawString($"Freight Terms: {freightTerms}", infoFont, PdfBrushes.Black,
                new PointF(marginX, yPosition));
            yPosition += 15;
        }


        if (!string.IsNullOrWhiteSpace(header.GeneralNotes))
        {
            string generalNotes = header.GeneralNotes;

            RectangleF headerBounds = new RectangleF(marginX, yPosition, clientSize.Width - (2 * marginX), 70);
            PdfStringFormat format = new PdfStringFormat { LineSpacing = 3, WordWrap = PdfWordWrapType.Word };
            graphics.DrawString($"Notes: {generalNotes}", smallFont, PdfBrushes.Black, headerBounds, format);
            yPosition += 30; // More spacing for clarity
        }


        // string disclaimer = "*** This document is for reference only and does not replace signed contract ***";
        //  graphics.DrawString($"{disclaimer}", smallFont, PdfBrushes.Black, new PointF(marginX, yPosition));
        //  yPosition += 15;

        string contactString = string.Empty;





        // Draw contract details

        string disclaimer = @"
All prices and product details listed in this agreement are based on the most current information available at the time of issuance. 
While we strive for accuracy, typographical or clerical errors may occur. In the event of such errors, we reserve
the right to correct the pricing or product information at our discretion.

Prices and product availability are also subject to change at any time due to market conditions, supplier adjustments, or other unforeseen factors. Should any changes be necessary, we will provide notice as promptly as possible. This agreement is not intended to create any binding obligation beyond the terms and conditions set forth herein.
";



        string headerText =
            $"*** This document is for reference only and does not replace signed contract ***" +
            $"\n{disclaimer} " +
            $"\n{contactString}" +
            $"\nReference: {header.PCFTypeDescription} PCF {header.PcfNumber}";

        RectangleF headerBounds2 = new RectangleF(marginX, yPosition, clientSize.Width - (2 * marginX), 165);
        PdfStringFormat format2 = new PdfStringFormat { LineSpacing = 3, WordWrap = PdfWordWrapType.Word };
        graphics.DrawString(headerText, smallFont, PdfBrushes.Black, headerBounds2, format2);
        yPosition += 160; // More spacing for clarity






        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Create the data table
        PdfGrid grid = new PdfGrid();
        grid.Columns.Add(3);
        grid.Headers.Add(1);

        PdfGridRow headerRow = grid.Headers[0];
        headerRow.Cells[0].Value = "Item Number";
        headerRow.Cells[1].Value = "Description";
        headerRow.Cells[2].Value = "Price";





        foreach (var item in header.PCFLines)
        {
            PdfGridRow row = grid.Rows.Add();

            row.Cells[0].Value = item.ItemNum;
            row.Cells[1].Value = item.ItemDesc;
            row.Cells[2].Value = item.ApprovedPrice.ToString("C2");
        }



        grid.ApplyBuiltinStyle(PdfGridBuiltinStyle.PlainTable3);

        grid.RepeatHeader = true;
        grid.Draw(page, new PointF(0, yPosition + 20));

        MemoryStream stream = new MemoryStream();
        document.Save(stream);
        document.Close(true);
        stream.Position = 0;

        return stream;
    }



    public void SendPcfPdfEmailWithAttachmentDONOTUSE(PCFHeader header, string EmailAddress,
        string CCEmailAddress = null)
    {
        // Generate the PDF
        MemoryStream pdfStream = CreatePCFPDF(header);

        var subject = $"PCF: {header.PcfNumber} for Customer: {header.CustomerNumber} has been approved";
        var filename = $"PCF {header.PcfNumber}_{header.CustomerNumber}.pdf";
        var body = @"
            <p>We are pleased to inform you that the Pricing Control Form has been approved. Please find the attached document for your records.</p>
            <p>Please reach out to us if you have any questions after reviewing the PCF.</p>
            <p></p>
            <p>Best regards,</p>
            <p>Your friendly SalesOps team</p>
        ";

        // Create the email message
        MailMessage mail = new MailMessage();
        mail.From = new MailAddress("Sales-Ops@chapinmfg.com");

        // Normalize email list by replacing semicolons with commas
        string normalizedEmailList = EmailAddress.Replace(";", ",");

        // Add recipients (comma-separated)
        mail.To.Add(normalizedEmailList);

        // Add CC recipients if provided
        if (!string.IsNullOrEmpty(CCEmailAddress))
        {
            string normalizedCCEmailList = CCEmailAddress.Replace(";", ",");
            mail.CC.Add(normalizedCCEmailList);
        }

        mail.Subject = subject;
        mail.Body = body;
        mail.IsBodyHtml = true;

        // Attach the PDF
        pdfStream.Position = 0; // Reset the stream position to the beginning
        Attachment attachment = new Attachment(pdfStream, filename, "application/pdf");
        mail.Attachments.Add(attachment);

        // Configure the SMTP client
        var smtpPassword =
            _configuration
                ["SmtpSettings:Password"]; // reads from appsettings.json or secrets.json in my APPDATA folder for Dev only
        // _logger.LogInformation("The smtpPassword appsettings or secrets is {smtpPassword}", smtpPassword);

        if (string.IsNullOrEmpty(smtpPassword))
        {
            smtpPassword = Environment.GetEnvironmentVariable("SMTPpassword"); // set in IIS under configuration editor
            _logger.LogInformation("The Env smtpPassword is {smtpPassword}", smtpPassword);

            smtpPassword = string.Empty;
            // _logger.LogInformation("The  Env override smtpPassword is {smtpPassword}", smtpPassword);

        }

        if (string.IsNullOrEmpty(smtpPassword))
        {
            smtpPassword = "D,$k3brpg8qrJ;_~";
            //  _logger.LogInformation("The hardcodded smtpPassword is {smtpPassword}", smtpPassword);

        }

        var smtpClient = new SmtpClient("CIIEXCH16")
        {
            Port = 25, // Typically, port 25 is used for Exchange Server
            Credentials = new NetworkCredential("Administrator", smtpPassword),
            EnableSsl = false, // Usually, SSL is not used for local Exchange Servers
        };

        // _logger.LogInformation("The final smtpPassword is {smtpPassword}", smtpPassword);

        // Send the email
        try
        {
            smtpClient.Send(mail);
        }
        catch (Exception ex)
        {
            // Handle the exception (log it, show a message, etc.)
            Console.WriteLine("Error sending email: " + ex.Message);
        }
        finally
        {
            // Clean up
            attachment.Dispose();
            pdfStream.Dispose();
        }
    }


    public byte[] ExportPcfHeaderToPdfNotSoNice(PCFHeader pcfHeader)
    {
        var wwwrootpath = _hostingEnvironment.WebRootPath;
        var _pdfHeaderImagePath = Path.Combine(wwwrootpath, "images", "pdfheader.png");

        // Create a new PDF document.
        using (PdfDocument pdfDocument = new PdfDocument())
        {
            // Add the first page.
            PdfPage page = pdfDocument.Pages.Add();
            PdfGraphics graphics = page.Graphics;

            // --------------------------------------------------------------------------------
            // Configure Header Template (Top): Draws "pdfheader.png" on the first page.
            // --------------------------------------------------------------------------------
            RectangleF headerBounds = new RectangleF(0, 0, page.GetClientSize().Width, 50);
            PdfPageTemplateElement headerTemplate = new PdfPageTemplateElement(headerBounds);
            using (FileStream imageStream = new FileStream(_pdfHeaderImagePath, FileMode.Open, FileAccess.Read))
            {
                PdfBitmap headerImage = new PdfBitmap(imageStream);
                // Adjust the image size as needed (here 100 x 50).
                headerTemplate.Graphics.DrawImage(headerImage, new PointF(0, 0), new SizeF(100, 50));
            }
            pdfDocument.Template.Top = headerTemplate;

            // --------------------------------------------------------------------------------
            // Configure Footer Template (Bottom): Displays "Page x of y".
            // --------------------------------------------------------------------------------
            RectangleF footerBounds = new RectangleF(0, 0, page.GetClientSize().Width, 50);
            PdfPageTemplateElement footerTemplate = new PdfPageTemplateElement(footerBounds);
            PdfFont footerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 7);
            PdfBrush footerBrush = new PdfSolidBrush(Color.Black);
            PdfPageNumberField pageNumberField = new PdfPageNumberField(footerFont, footerBrush);
            PdfPageCountField pageCountField = new PdfPageCountField(footerFont, footerBrush);
            PdfCompositeField compositeField = new PdfCompositeField(footerFont, footerBrush, "Page {0} of {1}", pageNumberField, pageCountField);
            compositeField.Bounds = footerBounds;
            // Position the composite field appropriately within the footer template.
            compositeField.Draw(footerTemplate.Graphics, new PointF(page.GetClientSize().Width - 100, 30));
            pdfDocument.Template.Bottom = footerTemplate;

            // --------------------------------------------------------------------------------
            // Prepare to draw content: Leave room for header & footer.
            // --------------------------------------------------------------------------------
            float margin = 40;
            // Increase top margin to avoid the header template area
            float yPosition = margin + 60;
            float lineSpacing = 20;

            // Define fonts.
            PdfFont labelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12);
            PdfFont tableHeaderFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
            PdfFont tableCellFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12);

            // Helper function to draw a label and its value.
            void DrawDetail(string label, string value)
            {
                graphics.DrawString($"{label}: {value}", labelFont, PdfBrushes.Black, margin, yPosition);
                yPosition += lineSpacing;
            }

            // Draw the PCF header details.
            DrawDetail("Customer Number", pcfHeader.CustomerNumber);
            DrawDetail("Customer Name", pcfHeader.CustomerName);
            DrawDetail("PCF Number", pcfHeader.PcfNumber);
            DrawDetail("PCF Type", pcfHeader.PCFTypeDescription);
            DrawDetail("Start Date", pcfHeader.StartDate.ToString("MM-dd-yyyy"));
            DrawDetail("End Date", pcfHeader.EndDate.ToString("MM-dd-yyyy"));
            DrawDetail("Bill To Address", $"{pcfHeader.BillToAddress}, {pcfHeader.BillToCity}, {pcfHeader.BTState} {pcfHeader.BTZip}");
            DrawDetail("Buying Group", pcfHeader.BuyingGroup);
            if (pcfHeader.PcfType == "PD" || pcfHeader.PcfType == "PW")
            {
                DrawDetail("Promo Payment Terms", pcfHeader.PromoPaymentTermsDescription);
                DrawDetail("Promo Freight Terms", pcfHeader.PromoFreightTerms);
                DrawDetail("Promo Freight Minimums", pcfHeader.PromoFreightMinimums);
            }
            DrawDetail("Rep Code", pcfHeader.RepCode);
            DrawDetail("Rep Name", pcfHeader.RepName);
            DrawDetail("Rep Agency", pcfHeader.RepAgency);
            DrawDetail("General Notes", pcfHeader.GeneralNotes);
            DrawDetail("Sales Manager", pcfHeader.SalesManager);
            DrawDetail("Approval Date", pcfHeader.VPSalesDate.HasValue ? pcfHeader.VPSalesDate.Value.ToString("MM-dd-yyyy") : "N/A");

            // Add some spacing before the table.
            yPosition += 20;

            // --------------------------------------------------------------------------------
            // Draw the table header for PCF lines.
            // --------------------------------------------------------------------------------
            string[] tableHeaders = { "Item #", "Item Description", "Approved Price", "Item Status" };
            float[] columnWidths = { 70, 250, 100, 80 };
            float tableWidth = columnWidths.Sum();
            float rowHeight = 20;
            float currentX = margin;

            // Draw table header rectangle.
            graphics.DrawRectangle(PdfPens.Black, PdfBrushes.LightGray, currentX, yPosition, tableWidth, rowHeight);
            for (int i = 0; i < tableHeaders.Length; i++)
            {
                graphics.DrawString(tableHeaders[i], tableHeaderFont, PdfBrushes.Black,
                    currentX + 2, yPosition + 2);
                currentX += columnWidths[i];
            }
            yPosition += rowHeight;

            // --------------------------------------------------------------------------------
            // Draw each row for the PCF lines. Add new pages as needed.
            // --------------------------------------------------------------------------------
            foreach (var line in pcfHeader.PCFLines)
            {
                // Check if adding the next row would exceed the usable page height.
                if (yPosition + rowHeight > page.GetClientSize().Height - margin - 50)
                {
                    // Add a new page and reset the graphics & yPosition.
                    page = pdfDocument.Pages.Add();
                    graphics = page.Graphics;
                    yPosition = margin + 60;

                    // Redraw the table header on the new page.
                    currentX = margin;
                    graphics.DrawRectangle(PdfPens.Black, PdfBrushes.LightGray, currentX, yPosition, tableWidth, rowHeight);
                    for (int i = 0; i < tableHeaders.Length; i++)
                    {
                        graphics.DrawString(tableHeaders[i], tableHeaderFont, PdfBrushes.Black,
                            currentX + 2, yPosition + 2);
                        currentX += columnWidths[i];
                    }
                    yPosition += rowHeight;
                }

                // Draw row background and borders.
                currentX = margin;
                graphics.DrawRectangle(PdfPens.Black, currentX, yPosition, tableWidth, rowHeight);
                graphics.DrawString(line.ItemNum, tableCellFont, PdfBrushes.Black,
                    currentX + 2, yPosition + 2);
                currentX += columnWidths[0];

                graphics.DrawString(line.ItemDesc, tableCellFont, PdfBrushes.Black,
                    currentX + 2, yPosition + 2);
                currentX += columnWidths[1];

                graphics.DrawString(line.ApprovedPrice.ToString("C2"), tableCellFont, PdfBrushes.Black,
                    currentX + 2, yPosition + 2);
                currentX += columnWidths[2];

                graphics.DrawString(line.ItemStatus, tableCellFont, PdfBrushes.Black,
                    currentX + 2, yPosition + 2);
                yPosition += rowHeight;
            }

            // --------------------------------------------------------------------------------
            // Save the document into a MemoryStream and return the result.
            // --------------------------------------------------------------------------------
            using (MemoryStream stream = new MemoryStream())
            {
                pdfDocument.Save(stream);
                return stream.ToArray();
            }
        }
    }


    public byte[] ExportPcfHeaderToPdf2(PCFHeader pcfHeader)
    {
        var wwwrootpath = _hostingEnvironment?.WebRootPath ?? string.Empty;
        var _pdfHeaderImagePath = Path.Combine(wwwrootpath, "images", "pdfheader.png");

        using (PdfDocument pdfDocument = new PdfDocument())
        {
            PdfPage page = pdfDocument.Pages.Add();
            PdfGraphics graphics = page.Graphics;
            SizeF clientSize = page.GetClientSize();

            // --- Header & Footer Setup ---
            // (Keep your existing Header/Footer setup logic here)
            // ... (Header image drawing) ...

            // Correction: Measure string using the font
            RectangleF footerBounds = new RectangleF(0, 0, clientSize.Width, 30);
            PdfPageTemplateElement footerTemplate = new PdfPageTemplateElement(footerBounds);
            PdfFont footerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 8);
            PdfBrush footerBrush = PdfBrushes.Black;
            PdfPageNumberField pageNumberField = new PdfPageNumberField(footerFont, footerBrush);
            PdfPageCountField pageCountField = new PdfPageCountField(footerFont, footerBrush);
            PdfCompositeField compositeField = new PdfCompositeField(footerFont, footerBrush, "Page {0} of {1}", pageNumberField, pageCountField);
            compositeField.Bounds = footerBounds; // Set bounds before drawing

            // Correction: Measure string using the font for positioning
            string sampleFooterText = "Page 99 of 99"; // Use a representative string
            SizeF footerTextSize = footerFont.MeasureString(sampleFooterText);
            float footerX = (clientSize.Width - footerTextSize.Width) / 2;
            // Correction: Use PdfStringFormat for alignment within the Draw method if needed,
            // or calculate position manually as done here. Draw within the template bounds.
            // Draw slightly above the bottom edge of the footer bounds.
            compositeField.Draw(footerTemplate.Graphics, new PointF(footerX, footerBounds.Height - footerTextSize.Height - 5));
            pdfDocument.Template.Bottom = footerTemplate;
            // --- End Header & Footer Setup ---


            // --- Content Setup ---
            float margin = 40;
            float currentY = (pdfDocument.Template.Top?.Height ?? 0) + 15;
            float contentWidth = clientSize.Width - (2 * margin);
            float lineSpacing = 5;
            float sectionSpacing = 25;
            float labelValueSpacing = 2;

            // Fonts
            PdfFont titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 16, PdfFontStyle.Bold);
            PdfFont labelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
            PdfFont valueFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
            PdfFont tableHeaderFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
            PdfFont tableCellFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);

            // Correction: Ensure using Syncfusion.Pdf.Graphics; for PdfStringFormat
            PdfStringFormat leftAlignFormat = new PdfStringFormat(PdfTextAlignment.Left, PdfVerticalAlignment.Middle);
            PdfStringFormat rightAlignFormat = new PdfStringFormat(PdfTextAlignment.Right, PdfVerticalAlignment.Middle);


            // --- Helper Function DrawLabelAndValue (remains the same) ---
            float DrawLabelAndValue(PdfGraphics gfx, string label, string value, PdfFont lblFont, PdfFont valFont, float x, float y, float maxWidth)
            {
                // ... (Implementation from previous answer is likely okay) ...
                if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(value)) return y;
                float startY = y;
                if (!string.IsNullOrEmpty(label))
                {
                    gfx.DrawString(label, lblFont, PdfBrushes.Black, x, startY);
                    startY += lblFont.MeasureString(label).Height + labelValueSpacing;
                }
                if (!string.IsNullOrEmpty(value))
                {
                    PdfTextElement textElement = new PdfTextElement(value, valFont, PdfBrushes.Black);
                    // Draw relative to the page, constrained by width and available height
                    PdfLayoutResult result = textElement.Draw(page, new RectangleF(x, startY, maxWidth, clientSize.Height - startY - (pdfDocument.Template.Bottom?.Height ?? 0)));
                    startY = result.Bounds.Bottom;
                }
                return startY + lineSpacing;
            }
            // --- End Helper ---

            // --- 1. PCF Header Details Section (Draw using helper - remains the same) ---
            graphics.DrawString("PCF Header Details", titleFont, PdfBrushes.Black, margin, currentY);
            currentY += titleFont.MeasureString("PCF Header Details").Height + 15;

            float col1X = margin;
            float col2X = margin + contentWidth / 3 + 10;
            float col3X = margin + (contentWidth / 3 * 2) + 20;
            float colWidth = contentWidth / 3 - 15;

            float y1 = currentY, y2 = currentY, y3 = currentY;
            // ... (Calls to DrawLabelAndValue for all header fields as before) ...
            // Column 1
            y1 = DrawLabelAndValue(graphics, "Customer Number:", pcfHeader.CustomerNumber, labelFont, valueFont, col1X, y1, colWidth);
            y1 = DrawLabelAndValue(graphics, "PCF Type:", pcfHeader.PCFTypeDescription, labelFont, valueFont, col1X, y1, colWidth);
            string billToFullAddress = $"{pcfHeader.BillToAddress}, {pcfHeader.BillToCity}, {pcfHeader.BTState} {pcfHeader.BTZip}";
            y1 = DrawLabelAndValue(graphics, "Bill To Address:", billToFullAddress, labelFont, valueFont, col1X, y1, colWidth);
            y1 = DrawLabelAndValue(graphics, "Buying Group:", pcfHeader.BuyingGroup, labelFont, valueFont, col1X, y1, colWidth);
            y1 = DrawLabelAndValue(graphics, "Rep Code:", pcfHeader.RepCode, labelFont, valueFont, col1X, y1, colWidth);
            y1 = DrawLabelAndValue(graphics, "General Notes:", pcfHeader.GeneralNotes, labelFont, valueFont, col1X, y1, colWidth);
            y1 = DrawLabelAndValue(graphics, "Sales Manager:", pcfHeader.SalesManager, labelFont, valueFont, col1X, y1, colWidth);

            // Column 2
            y2 = DrawLabelAndValue(graphics, "Customer Name:", pcfHeader.CustomerName, labelFont, valueFont, col2X, y2, colWidth);
            y2 = DrawLabelAndValue(graphics, "Start Date:", pcfHeader.StartDate.ToString("MM-dd-yyyy"), labelFont, valueFont, col2X, y2, colWidth);
            y2 += (labelFont.MeasureString("X").Height + labelValueSpacing + lineSpacing) * 2; // Spacers
            y2 = DrawLabelAndValue(graphics, "Rep Name:", pcfHeader.RepName, labelFont, valueFont, col2X, y2, colWidth);
            y2 += labelFont.MeasureString("X").Height + labelValueSpacing + lineSpacing; // Spacer
            y2 = DrawLabelAndValue(graphics, "Approval Date:", pcfHeader.VPSalesDate.HasValue ? pcfHeader.VPSalesDate.Value.ToString("MM-dd-yyyy") : "N/A", labelFont, valueFont, col2X, y2, colWidth);

            // Column 3
            y3 = DrawLabelAndValue(graphics, "PCF Number:", pcfHeader.PcfNumber, labelFont, valueFont, col3X, y3, colWidth);
            y3 = DrawLabelAndValue(graphics, "End Date:", pcfHeader.EndDate.ToString("MM-dd-yyyy"), labelFont, valueFont, col3X, y3, colWidth);
            y3 += (labelFont.MeasureString("X").Height + labelValueSpacing + lineSpacing) * 2; // Spacers
            y3 = DrawLabelAndValue(graphics, "Rep Agency:", pcfHeader.RepAgency, labelFont, valueFont, col3X, y3, colWidth);


            // Conditional Promo Fields
            float conditionalY = Math.Max(y1, Math.Max(y2, y3)) + 5;
            if (pcfHeader.PcfType == "PD" || pcfHeader.PcfType == "PW")
            {
                conditionalY = DrawLabelAndValue(graphics, "Promo Payment Terms:", pcfHeader.PromoPaymentTermsDescription, labelFont, valueFont, col1X, conditionalY, contentWidth);
                conditionalY = DrawLabelAndValue(graphics, "Promo Freight Terms:", pcfHeader.PromoFreightTerms, labelFont, valueFont, col1X, conditionalY, contentWidth);
                conditionalY = DrawLabelAndValue(graphics, "Promo Freight Minimums:", pcfHeader.PromoFreightMinimums, labelFont, valueFont, col1X, conditionalY, contentWidth);
            }

            currentY = conditionalY + sectionSpacing;
            // --- End Header Details ---


            // --- 2. PCF Lines Section ---
            graphics.DrawString("PCF Lines", titleFont, PdfBrushes.Black, margin, currentY);
            currentY += titleFont.MeasureString("PCF Lines").Height + 15;

            // Check for page break before table
            float potentialTableStartY = currentY;
            // Estimate header height + a row
            float estimatedTableHeight = tableHeaderFont.Height + tableCellFont.Height + 10;
            if (potentialTableStartY + estimatedTableHeight > clientSize.Height - (pdfDocument.Template.Bottom?.Height ?? 0) - margin)
            {
                page = pdfDocument.Pages.Add();
                graphics = page.Graphics; // Get graphics for the new page
                currentY = (pdfDocument.Template.Top?.Height ?? 0) + 15; // Reset Y
                                                                         // Redraw title on new page
                graphics.DrawString("PCF Lines", titleFont, PdfBrushes.Black, margin, currentY);
                currentY += titleFont.MeasureString("PCF Lines").Height + 15;
                potentialTableStartY = currentY;
            }


            // --- Use PdfGrid for Table ---
            if (pcfHeader.PCFLines != null && pcfHeader.PCFLines.Any())
            { PdfGrid grid = new PdfGrid();
            grid.Columns.Add(3);
            grid.Headers.Add(1);

            PdfGridRow headerRow = grid.Headers[0];
            headerRow.Cells[0].Value = "Item Number";
            headerRow.Cells[1].Value = "Description";
            headerRow.Cells[2].Value = "Price";





            foreach (var item in pcfHeader.PCFLines)
            {
                PdfGridRow row = grid.Rows.Add();

                row.Cells[0].Value = item.ItemNum;
                row.Cells[1].Value = item.ItemDesc;
                row.Cells[2].Value = item.ApprovedPrice.ToString("C2");
            }

            int yPosition = 320;
            // grid.ApplyBuiltinStyle(PdfGridBuiltinStyle.GridTable4Accent1);
            grid.ApplyBuiltinStyle(PdfGridBuiltinStyle.PlainTable3);

            grid.RepeatHeader = true;
            grid.Draw(page, new PointF(0, yPosition + 20));


        }

            else
            {
                graphics.DrawString("No PCF lines available.", valueFont, PdfBrushes.Gray, margin, currentY);
            }


            // --- Save Document ---
            using (MemoryStream stream = new MemoryStream())
            {
                pdfDocument.Save(stream);
                stream.Position = 0;
                return stream.ToArray();
            }
        }
    }

















}

