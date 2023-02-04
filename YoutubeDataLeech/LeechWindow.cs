﻿using Newtonsoft.Json.Linq;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

/* 
 * Created in a few minutes by @tpbeldie for personal usage.
 * F^$# Youtube for removing any details of a video once removed. 
 */

namespace YoutubeDataLeech
{
    public partial class LeechWindow : Form
    {

        private int m_vidoesCount;
        private string m_channelName = string.Empty;
        private int m_videosLeft;
        private string m_channelId;
        private WebClient m_webClient = new WebClient();

        public LeechWindow() {
            InitializeComponent();
            comboBox1.SelectedIndex = 0;
            dataGridView1.RowTemplate.Height = 100;
            // Tests examples.
            // Testing for HDSounDI
            // Channel Id: UC26zQlW7dTNcyp9zKHVmv4Q 
            // Playlist Id: PLHB7pQtzGtiXw_toturl3vfPw0KJwIfdd
            textBox3.Text = "API_KEY_HERE";
        }

        private void button1_Click(object sender, EventArgs e) {
            dataGridView1.Rows.Clear();
            // By channel name. Example: @HDSounDI, HDSounDI
            if (comboBox1.SelectedIndex == 0) {
                var channelResponse = m_webClient.DownloadString("https://www.youtube.com/@" + textBox1.Text.Trim('@'));
                Regex pattern = new Regex(@"channelId"" content=""(.*?)""");
                var match = pattern.Match(channelResponse);
                m_channelId = match.Groups[1].Value;
            }
            try {
                m_vidoesCount = 0;
                string baseUrl = "https://www.googleapis.com/youtube/v3/";
                string apiKey = textBox3.Text;
                string playlist; JObject json; string url; string jsonString;
                // :: By channel id. 
                if (comboBox1.SelectedIndex == 0 || comboBox1.SelectedIndex == 1) {
                    if (string.IsNullOrEmpty(m_channelId)) {
                        m_channelId = textBox1.Text;
                    }
                    // Retrieve whole content uploads playlist.  
                    url = $"{baseUrl}channels?id={m_channelId}&part=contentDetails&key={apiKey}";
                    jsonString = m_webClient.DownloadString(url);
                    json = JObject.Parse(jsonString);
                    playlist = json["items"][0]["contentDetails"]["relatedPlaylists"]["uploads"].ToString();
                }
                else {
                    /// :: By playlist id.
                    /// Some channels, such as HDSounDI, have manual playlists with names like 'Recent Uploads' 
                    /// containing all videos from the channel, displaying a larger amount than 
                    /// the playlist generated by Youtube for [All Uploads Playlist]. 
                    /// So you would think using this using playlist id will retrieve more videos... That's incorrect.
                    /// That is only because the playlist counts the hidden/unavailable/private videos. 
                    /// So the result would be the same, or in fact, even worse because of possibility of human erors, so some would be skipped.
                    /// Fetch by 'channel id' for a complete valid data gathering of publicly available videos, unless you want a specific playlist . 
                    playlist = textBox1.Text;
                }
                url = $"{baseUrl}playlistItems?part=snippet&playlistId={playlist}&maxResults=50&key={apiKey}";
                jsonString = m_webClient.DownloadString(url);
                json = JObject.Parse(jsonString);
                m_videosLeft = int.Parse(json["pageInfo"]["totalResults"].ToString());
                AppendResultsInGrid(json);
                while (json["nextPageToken"] != null) {
                    url = $"{url}&pageToken={json["nextPageToken"]}";
                    jsonString = m_webClient.DownloadString(url);
                    json = JObject.Parse(jsonString);
                    AppendResultsInGrid(json);
                }
                if (m_vidoesCount > 0) {
                    Text = $"Done! Succesfully fetched {m_vidoesCount} videos from the channel {m_channelName} - [{m_channelId}]";
                }
                else {
                    Text = $"Sorry! Could not fetch a single video from the channel {m_channelName} - [{m_channelId}]";
                }
                MessageBox.Show(Text);

            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message.ToString());
            }
        }

        private void AppendResultsInGrid(JObject json) {
            foreach (var video in json["items"]) {
                string Thumbnail = video["snippet"]?["thumbnails"]?["default"]?["url"]?.ToString();
                if (Thumbnail is null) {
                    // Ghost number. The videos are unavailable/hidden.
                    continue;
                }
                string Title = video["snippet"]["title"].ToString();
                string Id = video["snippet"]["resourceId"]["videoId"].ToString();
                string FullLink = $"https://www.youtube.com/watch?v=" + Id;
                string Description = video["snippet"]["description"].ToString();
                string Uploader = m_channelName = video["snippet"]["channelTitle"].ToString();
                string UploadedAt = video["snippet"]["publishedAt"].ToString();
                Bitmap bmp;
                if (!string.IsNullOrEmpty(Thumbnail)) {
                    WebRequest request = WebRequest.Create(Thumbnail);
                    WebResponse response = request.GetResponse();
                    Stream responseStream = response.GetResponseStream();
                    bmp = new Bitmap(responseStream);
                    response.Dispose();
                    responseStream.Dispose();
                }
                else {
                    bmp = new Bitmap(2, 2);
                }
                dataGridView1.Rows.Add(bmp, Decode(Title), Decode(Id), Decode(FullLink), Decode(Description), Decode(Uploader), Decode(UploadedAt));
                Debug.WriteLine(Decode(Title) + "[" + Decode(Id) + "]" + " " + "Processed");
                Text = $"Fetched {++m_vidoesCount} videos from {Uploader} / {m_videosLeft - m_vidoesCount} Left";
                Invalidate();
            }
        }

        private string Decode(string input) {
            byte[] encodedBytes = Encoding.Default.GetBytes(input);
            return Encoding.UTF8.GetString(encodedBytes);
        }

        private void button2_Click(object sender, EventArgs e) {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "|.xls";
            saveFileDialog.FileName = m_channelName + " [" + textBox1.Text + "] - [" + DateTime.Now.ToShortDateString().Replace('/', '-') + "]";
            if (saveFileDialog.ShowDialog() == DialogResult.OK) {
                ExportToExcel(dataGridView1, saveFileDialog.FileName);
            }
        }

        private void ExportToExcel(DataGridView grid, string filePath) {
            int exported = 0;
            var workbook = new HSSFWorkbook();
            {
                var sheet = workbook.CreateSheet("Sheet1");
                var headerRow = sheet.CreateRow(0);
                for (var col = 0; col < grid.Columns.Count - 1; col++) {
                    var headerCell = headerRow.CreateCell(col);
                    headerCell.SetCellValue(grid?.Columns[col]?.HeaderText);
                }
                for (var row = 0; row < grid.Rows.Count - 1; row++) {
                    var dataRow = sheet.CreateRow(row + 1); // start from row 1, because header row is at row 0
                    for (var col = 0; col < grid.Columns.Count; col++) {
                        var dataCell = dataRow.CreateCell(col);
                        if (grid?.Columns?[col].CellType == typeof(DataGridViewImageCell)) {
                            var image = (Bitmap)grid.Rows[row].Cells[col].Value;
                            if (image != null) {
                                var imageByteArray = ImageToByteArray(image);
                                var pictureIdx = workbook.AddPicture(imageByteArray, PictureType.PNG);
                                var anchor = new HSSFClientAnchor(0, 0, 0, 0, col, row + 1, col + 1, row + 2);
                                var pict = sheet.CreateDrawingPatriarch().CreatePicture(anchor, pictureIdx);
                                image.Dispose();
                            }
                        }
                        else {
                            dataCell.SetCellValue(grid?.Rows?[row]?.Cells[col]?.Value?.ToString());
                        }
                    }
                    exported++;
                }
                using (var file = new FileStream(filePath, FileMode.Create)) {
                    workbook.Write(file);
                }
                MessageBox.Show($"Done! Succesfully exported {exported} videos from the channel {m_channelName} - [{m_channelId}] \r\n File Created -> {filePath}");
            }
        }

        private static byte[] ImageToByteArray(Image image) {
            using (var ms = new MemoryStream()) {
                image.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e) {
            if (comboBox1.SelectedIndex == 0) {
                textBox1.Text = "Insert channel name. Example: @HDSounDI or HDSounDI";
            }
            else if (comboBox1.SelectedIndex == 1) {
                textBox1.Text = "Insert channel id. Example: UC26zQlW7dTNcyp9zKHVmv4Q";
            }
            else if (comboBox1.SelectedIndex == 2) {
                textBox1.Text = "Insert playlist id. Example: PLHB7pQtzGtiXw_toturl3vfPw0KJwIfdd";
            }
        }
    }
}
