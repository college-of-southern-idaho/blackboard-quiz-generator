<%@ Page Language="C#" AutoEventWireup="true" Src="Default.aspx.cs" Inherits="_Default" validateRequest="True" %>

<%@ Import Namespace="System.IO" %>
    <form id="form1" runat="server">
        <table width="98%" border="0" cellspacing="0" cellpadding="0" align="center">
            <tr>
                <td>
                    <div style="width:600px; margin-left:auto; margin-right:auto; display:block;">
                    <h2>Blackboard Quiz Generator</h2>
                    <p style="font-weight: bold; color: #cc3300;">The CSI Blackboard Quiz Generator is available for the  public to use free of charge. Please note that CSI transitioned from Blackboard  to Canvas in 2016 and the Quiz Generator is no longer being updated. The Quiz  Generator will, however, continue to be available on the csi.edu website for  the time being. This tool is provided as-is with no guarantee as to its  accuracy or availability, and CSI does not provide support for non-CSI  personnel. The source code for the Quiz Generator is currently not available,  but it may be offered as an open source solution in the future. If you would  like to be notified if/when it is provided via open source, please send an  email to <a href="mailto:csiwebmaster@csi.edu" style="color: #cc3300; text-decoration: underline;">csiwebmaster@csi.edu</a>.</p> 
					<p style="padding:0;margin:0;">(<a target="_blank" href="doc.html">Documentation</a>)</p>
                        <p>
						Quiz Name:
                        <asp:TextBox ID="quizname" runat="server" Columns="30"></asp:TextBox><br />
						</p>
						<p>
                        Type or paste in your questions:<br />
                        <textarea id="inputbox" runat="server" style="width: 550px; height: 400px"></textarea><br />
                        <asp:Button ID="Button1" runat="server" Text="Create Quiz" OnClick="Button1_Click" /><br />
                        <asp:Panel ID="resultspanel" runat="server" Height="35px" Width="550px" Visible="False">
                            <asp:Label ID="resultslabel" runat="server" Text="Your results are available&amp;nbsp;"></asp:Label>
                            <asp:Button ID="resultsbutton" runat="server" Text="here" OnClick="resultsbutton_Click" /></asp:Panel>

						</p>
                        <asp:Panel ID="testpanel" runat="server" Width="500px" Visible="False">
                            <asp:Label ID="outputlabel" runat="server" Text="Output:"></asp:Label><br />
                            <textarea id="outputbox" runat="server" style="width: 550px; height: 400px"></textarea><br />
                            <br />
                        <asp:Label ID="debuglabel" runat="server" Text="Debug:"></asp:Label>
                        <textarea id="debugbox" runat="server" style="width: 550px; height: 400px"></textarea>
                    </asp:Panel>
                        &nbsp;<br />
                        <br />
                    </div>
                </td>
            </tr>
        </table>
    </form>