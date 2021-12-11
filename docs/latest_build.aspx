<%@ Page Language="C#" %>
<%@ Import Namespace="System.IO" %>
<%@ Import Namespace="System.Linq" %>

<script runat="server">
  protected override void OnLoad(EventArgs e)
  {
      var page = new DirectoryInfo(@"C:\OneDrive\www\Pix2dSite\Builds").GetFiles("*.zip").OrderBy(x=>x.CreationTime).FirstOrDefault();
      if(page != null){
        Response.Redirect("http://pix2d.com/builds/" + page.Name);
      }
  }
</script>

<html>
<body>
    <h2>All Builds:</h2>
        <% foreach (var file in new DirectoryInfo(@"C:\OneDrive\www\Pix2dSite\Builds").GetFiles("*.zip")
            .OrderBy(x=>x.CreationTime)) { %>
            <%= file.Name %><br />
        <% } %>
        <br />
</body>
</html>