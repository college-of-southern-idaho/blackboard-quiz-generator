using System;
using System.IO;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using System.Collections;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Xml;
using System.Text;
using System.Threading;
using ICSharpCode.SharpZipLib.Checksums;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.GZip;



public class _Default : CSI_TemplatePage
{

    protected delegate void linehandler(string line, ref XmlElement node);
    protected delegate void linefinisher(ref XmlElement node);
    protected IDictionary qtypes;
    protected IDictionary qtype_finishers;
    protected XmlDocument doc;
    protected TextBox quizname;
    protected Label inputlabel;
    protected HtmlTextArea inputbox;
    protected Button Button1;
    protected Panel resultspanel;
    protected Label resultslabel;
    protected Button resultsbutton;
    protected Panel testpanel;
    protected Label outputlabel;
    protected HtmlTextArea outputbox;
    protected Label debuglabel;
    protected HtmlTextArea debugbox;
    protected int numquestions = 0;



    protected void Page_Load(object sender, EventArgs e)
    {
        /* We want case insensitive keys. */
        qtypes = new ListDictionary();
        //qtypes.Add("mc", new linehandler(default_handler));
        //qtypes.Add("tf", new linehandler(default_handler));
        qtypes.Add("essay", new linehandler(essay_handler));
        qtypes.Add("blank", new linehandler(blank_handler));
        qtypes.Add("order", new linehandler(order_handler));
        qtypes.Add("match", new linehandler(match_handler));

        qtype_finishers = new ListDictionary();
        qtype_finishers.Add("order", new linefinisher(order_finisher));
        qtype_finishers.Add("match", new linefinisher(match_finisher));

        /* resultspanel.Visible = false; */
    }

	protected string encode_smart_quotes (string text)
	{
		string result;
		
		result = text.Replace("“", "&#8220;");
		result = result.Replace("”", "&#8221;");
		result = result.Replace("‘", "&#8216;");
		result = result.Replace("’", "&#8217;");

		return result;
	}

    protected void appendquestion(XmlNode root, XmlElement curnode)
    {
        string qid;
        string qtype;
        XmlElement question;
        XmlNode child;


        debugbox.Value += "In appendquestion.\n";

        //debugbox.Value += "Here's the whole node: " + curnode.OuterXml + "\n";

        qid = "q" + curnode.GetAttribute("qnum");
        qtype = curnode.GetAttribute("qtype");

        if (qid == "q" || qtype == "")
        {
            debugbox.Value += "Couldn't append current question node.\n";
            return;
        }

        /* Append question to tree. */
        question = doc.CreateElement(qtype);
        root.AppendChild(question);
        question.SetAttribute("id", qid);

        /* Reparent all of the children to our new element. */
        while (curnode.HasChildNodes)
        {
            child = curnode.RemoveChild(curnode.FirstChild);
            question.AppendChild(child);
        }

        /* Append to the question list. */
        for (int i = 0; i < root.ChildNodes.Count; i++)
        {
            if (root.ChildNodes[i].Name == "QUESTIONLIST")
            {
                question = doc.CreateElement("QUESTION");
                root.ChildNodes[i].AppendChild(question);
                question.SetAttribute("id", qid);
                question.SetAttribute("class", qtype);
                return;
            }
        }
    }

    protected void adddates(XmlNode root)
    {
        XmlElement node;
        XmlElement node2;


        node = doc.CreateElement("DATES");
        root.AppendChild(node);
        node2 = doc.CreateElement("CREATED");
        node.AppendChild(node2);
        node2.SetAttribute("value", DateTime.UtcNow.ToString("u"));
        node2 = doc.CreateElement("UPDATED");
        node.AppendChild(node2);
        node2.SetAttribute("value", DateTime.UtcNow.ToString("u"));
    }

    protected void initpool(XmlNode root)
    {
        string name;
        XmlElement node;
        XmlElement node2;
        XmlNode textnode;


        node = doc.CreateElement("COURSEID");
        root.AppendChild(node);
        node.SetAttribute("value", "IMPORT");

        node = doc.CreateElement("TITLE");
        root.AppendChild(node);

        name = quizname.Text;
        if (name.Length < 1)
            name = "Blackboard Quiz";
        node.SetAttribute("value", name);

        node = doc.CreateElement("DESCRIPTION");
        root.AppendChild(node);

        node2 = doc.CreateElement("TEXT");
        node.AppendChild(node2);
        textnode = doc.CreateTextNode("Created by the CSI Blackboard Quiz Generator");
        node2.AppendChild(textnode);

        adddates(root);

        node = doc.CreateElement("QUESTIONLIST");
        root.AppendChild(node);
    }

    protected void resultsbutton_Click(object sender, EventArgs e)
    {
        string filename;
        FileStream fs;
        BinaryReader r;
        byte[] zipfile;


        debugbox.Value += "in resultsbutton_Click\n";

        if (Session["tmpzipfile"] == null)
        {
            debugbox.Value += "session var is null\n";
            return;
        }

        filename = (string)Session["tmpzipfile"];
        fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
        r = new BinaryReader(fs);
        zipfile = r.ReadBytes((int)fs.Length);

        Response.ContentType = "application/x-zip";
        Response.AddHeader("content-disposition", "attachment; filename=bbquiz.zip");
        Response.BinaryWrite(zipfile);

        r.Close();
        fs.Close();

        File.Delete(filename);
        Session["tmpzipfile"] = null;

        Response.End();
    }

    protected void Button1_Click(object sender, EventArgs e)
    {
        string text = inputbox.Value;
        string[] list;
        char[] delim = { '\n' };
        XmlNode root;
        XmlElement curnode;
        linehandler curhandler = null;
        linefinisher curfinisher = null;
        bool nohandler = true;


        doc = new XmlDocument();
        root = doc.CreateElement("POOL");
        doc.AppendChild(root);

        initpool(root);

        curnode = doc.CreateElement("QUESTION");
        curnode.SetAttribute("state", "null");

        outputbox.Value = "";
        debugbox.Value = "";

        list = text.Split(delim);

        foreach (string tmp in list)
        {
            debugbox.Value += tmp;
            /* Apply the handler */
            try
            {
                if (nohandler)
                    throw new System.InvalidOperationException("No handler yet.");

                curhandler(tmp, ref curnode);
            }
            catch (System.InvalidOperationException excp)
            {
                debugbox.Value += "Caught exception: " + excp.Message + "\n";
                if (!nohandler)
                {
                    debugbox.Value += "Finishing node: " + curnode + "\n";
                    if (curfinisher != null)
                    {
                        debugbox.Value += "Have a finisher callback.";
                        curfinisher(ref curnode);
                    }

                    appendquestion(root, curnode);
                    curnode = doc.CreateElement("question");
                    curnode.SetAttribute("state", "null");
                }

                /* Find a new handler. */
                try
                {
                    string qtype;
                    string restofline;
                    Regex findhandler = new Regex(@"^\s*(?<qtype>\w+)\s+(?<restofline>.*)$");
                    Match m = findhandler.Match(tmp);

                    /* Must be nicely formatted. */
                    if (m.Success)
                    {
                        qtype = m.Groups["qtype"].Value.ToLower().Trim();
                        restofline = m.Groups["restofline"].Value.Trim();

                        debugbox.Value += "qtype: " + qtype + " rest: " + restofline + "\n";

                        if (qtypes.Contains(qtype))
                        {
                            curhandler = (linehandler)qtypes[qtype];
                            if (qtype_finishers.Contains(qtype))
                                curfinisher = (linefinisher)qtype_finishers[qtype];
                            else
                                curfinisher = null;


                            curnode.SetAttribute("qtype", qtype);
                            curhandler(restofline, ref curnode);
                            nohandler = false;

                        }
                        else
                        {
                            throw new System.InvalidOperationException("Couldn't find a matching question type for: " + qtype);
                        }
                    }
                    else
                    {
                        /* Try the default handler. */
                        Regex firstline = new Regex(@"^\s*(?<qnum>\d+)[\)\.]\s*(?<qtext>.+)$");
                        Match m2 = firstline.Match(tmp);
                        if (m2.Success)
                        {
                            curhandler = new linehandler(default_handler);
                            /* No default finisher callback. */
                            curfinisher = null;
                            curhandler(tmp, ref curnode);
                            nohandler = false;
                        }
                        else
                        {
                            nohandler = true;
                            throw new System.InvalidOperationException("Couldn't find a question type in line: (" + tmp + ")");
                        }
                    }
                }
                catch (System.InvalidOperationException excp2)
                {
                    debugbox.Value += "Caught exception2: " + excp2.Message + "\n";
                }
            }
        }

        debugbox.Value += "Finishing node: " + curnode + "\n";
        if (curfinisher != null)
        {
            debugbox.Value += "Have a finisher callback.\n";
            curfinisher(ref curnode);
        }
        appendquestion(root, curnode);

        /*
        XmlWriterSettings settings = new XmlWriterSettings();
        settings.Indent = true;
        settings.Encoding = System.Text.Encoding.UTF8;
        */

        //StringBuilder xmloutput = new StringBuilder();
        MemoryStream ms = new MemoryStream(1024);

        ms.Seek(0, SeekOrigin.Begin);

        //XmlWriter writer = XmlWriter.Create(xmloutput, settings);
        /*XmlWriter writer = XmlWriter.Create(ms, settings);*/
        XmlTextWriter writer = new XmlTextWriter(ms, System.Text.Encoding.UTF8);
        writer.Formatting = Formatting.Indented;
        writer.WriteStartDocument();

        doc.WriteTo(writer);
        writer.Flush();

        outputbox.Value += "Done with input...\n";
        //outputbox.Value += "doc: \n" + xmloutput.ToString() + "\n";

        debugbox.Value += "ms len: " + ms.Length + " pos: " + ms.Position + "\n";
        debugbox.Value += "ms capacity: " + ms.Capacity + "\n";
        UTF8Encoding encoding = new System.Text.UTF8Encoding();
        outputbox.Value += "doc: \n" + encoding.GetString(ms.ToArray()) + "\n";


        //outputbox.Value += "actual doc xml:\n" + doc.OuterXml + "\n";
        //write_files(xmloutput);
        write_files(ms);
        resultslabel.Text = "Your test seemed to have " + numquestions + " questions. The package is available: ";
        resultspanel.Visible = true;
    }

    protected void write_files(MemoryStream xmloutput)
    {
        string filename;
        string manifestxml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<manifest identifier=\"man00001\"><organization default=\"toc00001\"><tableofcontents identifier=\"toc00001\"/></organization><resources><resource baseurl=\"res00001\" file=\"res00001.dat\" identifier=\"res00001\" type=\"assessment/x-bb-pool\"/></resources></manifest>";
        ZipOutputStream s;
        ZipEntry entry;
        Crc32 crc = new Crc32();
        byte[] buffer;
        UTF8Encoding encoding = new System.Text.UTF8Encoding();
        //MemoryStream ms = new MemoryStream(4096);


        /* Zip file. */
        filename = Path.GetTempFileName();

        /* Not sure if this is the best way to do this. */
        if ((Session["tmpzipfile"]) != null)
        {
            debugbox.Value += "Deleting existing tmpfile: (" + Session["tmpzipfile"] + ")\n";
            File.Delete((string)Session["tmpzipfile"]);
        }
        Session["tmpzipfile"] = filename;
        debugbox.Value += "Created tmpfile: (" + Session["tmpzipfile"] + ")\n";

        s = new ZipOutputStream(new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Write));
        /*s = new ZipOutputStream(File.Create(filename);*/
        //s = new ZipOutputStream(ms);
        s.SetLevel(6);

        /* First file: imsmanifest.xml. */
        buffer = encoding.GetBytes(manifestxml);

        entry = new ZipEntry("imsmanifest.xml");
        entry.DateTime = DateTime.Now;
        entry.Size = buffer.Length;

        crc.Reset();
        crc.Update(buffer);
        entry.Crc = crc.Value;

        s.PutNextEntry(entry);

        s.Write(buffer, 0, buffer.Length);

        /* Second file: res00001.dat. */
        debugbox.Value += "In write\n";
        buffer = xmloutput.ToArray();

        debugbox.Value += "xml len:" + xmloutput.ToString().Length + "\n";
        debugbox.Value += "buffer len:" + buffer.Length + "\n";

        entry = new ZipEntry("res00001.dat");
        entry.DateTime = DateTime.Now;
        entry.Size = buffer.Length;

        crc.Reset();
        crc.Update(buffer);
        entry.Crc = crc.Value;

        s.PutNextEntry(entry);

        s.Write(buffer, 0, buffer.Length);

        s.Finish();
        s.Close();
    }

    protected void default_set_correct(string aid, XmlElement node)
    {
        XmlElement newnode;
        XmlElement gradable;


        gradable = do_get_gradable(node);

        newnode = doc.CreateElement("CORRECTANSWER");
        gradable.AppendChild(newnode);
        newnode.SetAttribute("answer_id", aid);
    }

    protected void default_check_multiple(XmlElement node)
    {
        XmlElement gradable;
        XmlNodeList correctlist;


        gradable = do_get_gradable(node);

        correctlist = gradable.GetElementsByTagName("CORRECTANSWER");
        if (correctlist.Count > 1)
        {
            debugbox.Value += "Found multiple correct answers, changing question types.\n";
            node.SetAttribute("qtype", "QUESTION_MULTIPLEANSWER");
        }
    }

    protected XmlElement do_get_gradable(XmlElement node)
    {
        XmlElement newnode;
        XmlElement gradable;
        XmlNodeList list;


        list = node.GetElementsByTagName("GRADABLE");
        if (list.Count < 1)
        {
            gradable = doc.CreateElement("GRADABLE");
            node.AppendChild(gradable);
            newnode = doc.CreateElement("FEEDBACK_WHEN_CORRECT");
            gradable.AppendChild(newnode);
            newnode.AppendChild(doc.CreateTextNode("Good work"));
            newnode = doc.CreateElement("FEEDBACK_WHEN_INCORRECT");
            gradable.AppendChild(newnode);
            newnode.AppendChild(doc.CreateTextNode("That's not correct"));
        }
        else
            gradable = (XmlElement)list[0];

        return gradable;
    }

    protected void default_do_tfanswer(string line, string tf, XmlElement node)
    {
        bool istrue = false;
        string qid;
        string aid;
        XmlElement answernode;
        XmlElement answertextnode;


        if (tf.Length < 1)
            throw new System.InvalidOperationException("True false question, error in tf string.");

        /* Ok, was it true or false? */
        if (tf.ToLower()[0] == 't')
            istrue = true;

        debugbox.Value += "tf: " + tf + "\n";

        /* True Answer, dates, and text. */
        answernode = doc.CreateElement("ANSWER");
        node.AppendChild(answernode);
        adddates(answernode);
        answertextnode = doc.CreateElement("TEXT");
        answernode.AppendChild(answertextnode);
        answertextnode.AppendChild(doc.CreateTextNode("True"));

        qid = "q" + node.GetAttribute("qnum");
        aid = qid + "_a1";
        answernode.SetAttribute("id", aid);
        answernode.SetAttribute("position", "1");

        if (istrue)
            default_set_correct(aid, node);

        /* False Answer, dates, and text. */
        answernode = doc.CreateElement("ANSWER");
        node.AppendChild(answernode);
        adddates(answernode);
        answertextnode = doc.CreateElement("TEXT");
        answernode.AppendChild(answertextnode);
        answertextnode.AppendChild(doc.CreateTextNode("False"));

        aid = qid + "_a2";
        answernode.SetAttribute("id", aid);
        answernode.SetAttribute("position", "2");

        if (!istrue)
            default_set_correct(aid, node);

        node.SetAttribute("qtype", "QUESTION_TRUEFALSE");

        /* Let the default handler know we're done with a T/F question. */
        node.SetAttribute("state", "tffinished");
    }

    protected void default_do_state(string line, string state, XmlElement node)
    {
        switch (state)
        {
            case "null":
                string qtext;
                XmlElement bodynode;
                XmlElement textnode;
                XmlElement flagsnode;
                XmlElement etcnode;


                if (Regex.IsMatch(line, @"^\s*$"))
                    return;

                Regex firstline = new Regex(@"^\s*(?<qnum>\d+)[\)\.]\s*(?<qtext>.+)$");
                Match m = firstline.Match(line);
                /* Must have a nicely formatted first line. */
                if (m.Success)
                {
                    qtext = m.Groups["qtext"].Value.Trim();
					qtext = encode_smart_quotes(qtext);
					/* qtext = Server.HtmlEncode(qtext); */
                    debugbox.Value += "New question num: (" + m.Groups["qnum"].Value + ")\n";
                    debugbox.Value += "New question text: (" + qtext + ")\n";
                    debugbox.Value += "encoded question text: (" + qtext + ")\n";
					
                    numquestions++;

                    node.SetAttribute("qnum", numquestions.ToString());
                    node.SetAttribute("qtext", qtext);
                }
                else
                    throw new System.InvalidOperationException("Line: (" + line + ") isn't a valid first line.");

                /* Default.  This may be overwritten. */
                node.SetAttribute("qtype", "QUESTION_ESSAY");
                adddates(node);

                /* Body tag and text. */
                bodynode = doc.CreateElement("BODY");
                node.AppendChild(bodynode);
                textnode = doc.CreateElement("TEXT");
                bodynode.AppendChild(textnode);
                textnode.AppendChild(doc.CreateTextNode(qtext));

                /* Flags subtree. */
                flagsnode = doc.CreateElement("FLAGS");
                bodynode.AppendChild(flagsnode);
                etcnode = doc.CreateElement("ISHTML");
                flagsnode.AppendChild(etcnode);
                etcnode.SetAttribute("value", "true");
                etcnode = doc.CreateElement("ISNEWLINELITERAL");
                flagsnode.AppendChild(etcnode);
                flagsnode.SetAttribute("value", "true");

                /* Next step... */
                node.SetAttribute("state", "answers");
                break;

            case "answers":
                string aid;
                string qid;
                string atext;
                string acorrect;
                XmlElement answernode;
                XmlElement answertextnode;
                XmlNodeList allanswers;


                /* Empty line means new question. */
                if (Regex.IsMatch(line, @"^\s*$"))
                    throw new System.InvalidOperationException("Empty line in answers state means new question.");

                /* See if this is actually a true/false question. */
                Regex tfanswer = new Regex(@"^\s*(?<truefalse>T|t|True|TRUE|true|F|f|False|FALSE|false)\s*$");
                Match mtf = tfanswer.Match(line);
                if (mtf.Success)
                {
                    debugbox.Value += "Found a T/F question.\n";
                    default_do_tfanswer(line, mtf.Groups["truefalse"].Value, node);
                    return;
                }

                Regex answerline = new Regex(@"^\s*(?<acorrect>\*)?(?<anum>(\d+|\w))(?<asep>[\)\.])\s*(?<atext>.+)$");
                Match m2 = answerline.Match(line);

                /* Must have a nicely formatted answer line. */
                if (m2.Success)
                {
                    atext = m2.Groups["atext"].Value.Trim();
					atext = encode_smart_quotes(atext);
					
                    acorrect = m2.Groups["acorrect"].Value.Trim();

                    debugbox.Value += "New answer correct: " + acorrect + "\n";
                    debugbox.Value += "New answer num: (" + m2.Groups["anum"].Value + ")\n";
                    debugbox.Value += "New answer text: (" + atext + ")\n";
                    debugbox.Value += "Answer separator: (" + m2.Groups["asep"].Value + ")\n";

                    /* Kind of difficult to distinguish between question and answer lines. */
                    /* This assumes they have different separators. */
/*
                    if (node.GetAttribute("asep") != m2.Groups["asep"].Value)
                    {
                        if (node.GetAttribute("asep") == "")
                            node.SetAttribute("asep", m2.Groups["asep"].Value);
                        else
                            throw new System.InvalidOperationException("Line: (" + line + ") looks like a new question.");
                    }
 */
                }
                else
                    throw new System.InvalidOperationException("Line: (" + line + ") isn't a valid answers line.");

                /* Answer, dates, and text. */
                answernode = doc.CreateElement("ANSWER");
                node.AppendChild(answernode);
                adddates(answernode);
                answertextnode = doc.CreateElement("TEXT");
                answernode.AppendChild(answertextnode);
                answertextnode.AppendChild(doc.CreateTextNode(atext));

                /* How many answers do we have for this question? */
                allanswers = node.GetElementsByTagName("ANSWER");
                debugbox.Value += "Number of answers: " + allanswers.Count + "\n";

                qid = "q" + node.GetAttribute("qnum");
                aid = qid + "_a" + allanswers.Count;
                answernode.SetAttribute("id", aid);
                answernode.SetAttribute("position", "" + allanswers.Count);

                /* Now that we have an answer, we're no longer an essay question. */
                node.SetAttribute("qtype", "QUESTION_MULTIPLECHOICE");

                if (acorrect == "*")
                    default_set_correct(aid, node);

                default_check_multiple(node);

                break;
            case "tffinished":
                throw new System.InvalidOperationException("In default handler after T/F question.");
            default:
                break;
        }
    }

    protected void default_handler(string line, ref XmlElement node)
    {
        int visited;
        string state = node.GetAttribute("state");


        debugbox.Value += "In default handler, state: " + state + " line: " + line + "\n";

        default_do_state(line, state, node);

        debugbox.Value += "Node visited: " + node.GetAttribute("visited") + "\n";

        try
        {
            visited = (int)Int32.Parse(node.GetAttribute("visited"));
        }
        catch (System.SystemException e)
        {
            visited = 0;
        }

        visited += 1;
        node.SetAttribute("visited", visited.ToString());
        debugbox.Value += "Set visited to: " + visited.ToString() + "\n";

        debugbox.Value += "Visited: " + visited + " times!\n";
    }

    protected void essay_handler(string line, ref XmlElement node)
    {
        string state = node.GetAttribute("state");

        debugbox.Value += "In essay handler, state: " + state + " line: " + line + "\n";

        switch (state)
        {
            case "null":
                string qtext;
                XmlElement bodynode;
                XmlElement textnode;
                XmlElement flagsnode;
                XmlElement etcnode;


                if (Regex.IsMatch(line, @"^\s*$"))
                    return;

                Regex firstline = new Regex(@"^\s*(?<qnum>\d+)[\)\.]\s*(?<qtext>.+)$");
                Match m = firstline.Match(line);
                /* Must have a nicely formatted first line. */
                if (m.Success)
                {
                    qtext = m.Groups["qtext"].Value.Trim();
					qtext = encode_smart_quotes(qtext);

                    debugbox.Value += "New question num: (" + m.Groups["qnum"].Value + ")\n";
                    debugbox.Value += "New question text: (" + qtext + ")\n";

                    numquestions++;

                    node.SetAttribute("qnum", numquestions.ToString());
                    node.SetAttribute("qtext", qtext);
                }
                else
                    throw new System.InvalidOperationException("Line: (" + line + ") isn't a valid first line.");

                /* Default.  This may be overwritten. */
                node.SetAttribute("qtype", "QUESTION_ESSAY");

                adddates(node);

                /* Body tag and text. */
                bodynode = doc.CreateElement("BODY");
                node.AppendChild(bodynode);
                textnode = doc.CreateElement("TEXT");
                bodynode.AppendChild(textnode);
                textnode.AppendChild(doc.CreateTextNode(qtext));

                /* Flags subtree. */
                flagsnode = doc.CreateElement("FLAGS");
                bodynode.AppendChild(flagsnode);
                etcnode = doc.CreateElement("ISHTML");
                flagsnode.AppendChild(etcnode);
                etcnode.SetAttribute("value", "true");
                etcnode = doc.CreateElement("ISNEWLINELITERAL");
                flagsnode.AppendChild(etcnode);
                flagsnode.SetAttribute("value", "true");

                /* Next step... */
                node.SetAttribute("state", "finished");
                break;

            case "finished":
                if (Regex.IsMatch(line, @"^\s*$"))
                    return;

                throw new System.InvalidOperationException("In essay handler, finished state.");
                break;

        }

    }

    protected void blank_handler(string line, ref XmlElement node)
    {
        string state = node.GetAttribute("state");


        debugbox.Value += "In blank handler, state: " + state + " line: " + line + "\n";

        switch (state)
        {
            case "null":
                string qtext;
                XmlElement bodynode;
                XmlElement textnode;
                XmlElement flagsnode;
                XmlElement etcnode;
                XmlElement gradable;


                if (Regex.IsMatch(line, @"^\s*$"))
                    return;

                Regex firstline = new Regex(@"^\s*(?<qnum>\d+)[\)\.]\s*(?<qtext>.+)$");
                Match m = firstline.Match(line);
                /* Must have a nicely formatted first line. */
                if (m.Success)
                {
                    qtext = m.Groups["qtext"].Value.Trim();
					qtext = encode_smart_quotes(qtext);

                    debugbox.Value += "New question num: (" + m.Groups["qnum"].Value + ")\n";
                    debugbox.Value += "New question text: (" + qtext + ")\n";

                    numquestions++;

                    node.SetAttribute("qnum", numquestions.ToString()); 
                    node.SetAttribute("qtext", qtext);
                }
                else
                    throw new System.InvalidOperationException("Line: (" + line + ") isn't a valid first line.");

                /* Default.  This may be overwritten. */
                node.SetAttribute("qtype", "QUESTION_FILLINBLANK");

                adddates(node);

                /* Body tag and text. */
                bodynode = doc.CreateElement("BODY");
                node.AppendChild(bodynode);
                textnode = doc.CreateElement("TEXT");
                bodynode.AppendChild(textnode);
                textnode.AppendChild(doc.CreateTextNode(qtext));

                /* Flags subtree. */
                flagsnode = doc.CreateElement("FLAGS");
                bodynode.AppendChild(flagsnode);
                etcnode = doc.CreateElement("ISHTML");
                flagsnode.AppendChild(etcnode);
                etcnode.SetAttribute("value", "true");
                etcnode = doc.CreateElement("ISNEWLINELITERAL");
                flagsnode.AppendChild(etcnode);
                flagsnode.SetAttribute("value", "true");

                /* Make sure we have a gradable node. */
                gradable = do_get_gradable(node);

                /* Next step... */
                node.SetAttribute("state", "answers");
                break;

            case "answers":
                string aid;
                string qid;
                string atext;
                string acorrect;
                XmlElement answernode;
                XmlElement answertextnode;
                XmlNodeList allanswers;


                /* Empty line means new question. */
                if (Regex.IsMatch(line, @"^\s*$"))
                    throw new System.InvalidOperationException("Empty line in answers state means new question.");

                Regex answerline = new Regex(@"^\s*(?<acorrect>\*)?(?<anum>(\d+|\w))(?<asep>[\)\.])\s*(?<atext>.+)$");
                Match m2 = answerline.Match(line);

                /* Must have a nicely formatted answer line. */
                if (m2.Success)
                {
                    atext = m2.Groups["atext"].Value.Trim();
					atext = encode_smart_quotes(atext);
                    acorrect = m2.Groups["acorrect"].Value.Trim();

                    debugbox.Value += "New answer correct: " + acorrect + "\n";
                    debugbox.Value += "New answer num: (" + m2.Groups["anum"].Value + ")\n";
                    debugbox.Value += "New answer text: (" + atext + ")\n";
                    debugbox.Value += "Answer separator: (" + m2.Groups["asep"].Value + ")\n";

                    /* Kind of difficult to distinguish between question and answer lines. */
                    /* This assumes they have different separators. */
/*
                    if (node.GetAttribute("asep") != m2.Groups["asep"].Value)
                    {
                        if (node.GetAttribute("asep") == "")
                            node.SetAttribute("asep", m2.Groups["asep"].Value);
                        else
                            throw new System.InvalidOperationException("Line: (" + line + ") looks like a new question.");
                    }
 */
                }
                else
                    throw new System.InvalidOperationException("Line: (" + line + ") isn't a valid answers line.");

                /* Answer, dates, and text. */
                answernode = doc.CreateElement("ANSWER");
                node.AppendChild(answernode);
                adddates(answernode);
                answertextnode = doc.CreateElement("TEXT");
                answernode.AppendChild(answertextnode);
                answertextnode.AppendChild(doc.CreateTextNode(atext));

                /* How many answers do we have for this question? */
                allanswers = node.GetElementsByTagName("ANSWER");
                debugbox.Value += "Number of answers: " + allanswers.Count + "\n";

                qid = "q" + node.GetAttribute("qnum");
                aid = qid + "_a" + allanswers.Count;
                answernode.SetAttribute("id", aid);
                answernode.SetAttribute("position", "" + allanswers.Count);
                break;
        }
    }

    protected void order_handler(string line, ref XmlElement node)
    {
        string state = node.GetAttribute("state");


        debugbox.Value += "In order handler, state: " + state + " line: " + line + "\n";

        switch (state)
        {
            case "null":
                string qtext;
                XmlElement bodynode;
                XmlElement textnode;
                XmlElement flagsnode;
                XmlElement etcnode;


                if (Regex.IsMatch(line, @"^\s*$"))
                    return;

                Regex firstline = new Regex(@"^\s*(?<qnum>\d+)[\)\.]\s*(?<qtext>.+)$");
                Match m = firstline.Match(line);
                /* Must have a nicely formatted first line. */
                if (m.Success)
                {
                    qtext = m.Groups["qtext"].Value.Trim();
					qtext = encode_smart_quotes(qtext);

                    debugbox.Value += "New question num: (" + m.Groups["qnum"].Value + ")\n";
                    debugbox.Value += "New question text: (" + qtext + ")\n";

                    numquestions++;

                    node.SetAttribute("qnum", numquestions.ToString());
                    node.SetAttribute("qtext", qtext);
                }
                else
                    throw new System.InvalidOperationException("Line: (" + line + ") isn't a valid first line.");

                /* Default.  This may be overwritten. */
                node.SetAttribute("qtype", "QUESTION_ORDER");

                adddates(node);

                /* Body tag and text. */
                bodynode = doc.CreateElement("BODY");
                node.AppendChild(bodynode);
                textnode = doc.CreateElement("TEXT");
                bodynode.AppendChild(textnode);
                textnode.AppendChild(doc.CreateTextNode(qtext));

                /* Flags subtree. */
                flagsnode = doc.CreateElement("FLAGS");
                bodynode.AppendChild(flagsnode);
                etcnode = doc.CreateElement("ISHTML");
                flagsnode.AppendChild(etcnode);
                etcnode.SetAttribute("value", "true");
                etcnode = doc.CreateElement("ISNEWLINELITERAL");
                flagsnode.AppendChild(etcnode);
                flagsnode.SetAttribute("value", "true");

                /* Next step... */
                node.SetAttribute("state", "answers");
                break;

            case "answers":
                string aid;
                string qid;
                string atext;
                string acorrect;
                XmlElement answernode;
                XmlElement answertextnode;
                XmlNodeList allanswers;


                /* Empty line means new question. */
                if (Regex.IsMatch(line, @"^\s*$"))
                    throw new System.InvalidOperationException("Empty line in answers state means new question.");

                Regex answerline = new Regex(@"^\s*(?<anum>(\d+|\w))(?<asep>[\)\.])\s*(?<atext>.+)$");
                Match m2 = answerline.Match(line);

                /* Must have a nicely formatted answer line. */
                if (m2.Success)
                {
                    atext = m2.Groups["atext"].Value.Trim();
					atext = encode_smart_quotes(atext);
                    acorrect = m2.Groups["acorrect"].Value.Trim();

                    debugbox.Value += "New answer num: (" + m2.Groups["anum"].Value + ")\n";
                    debugbox.Value += "New answer text: (" + atext + ")\n";
                    debugbox.Value += "Answer separator: (" + m2.Groups["asep"].Value + ")\n";

                    /* Kind of difficult to distinguish between question and answer lines. */
                    /* This assumes they have different separators. */
/*
                    if (node.GetAttribute("asep") != m2.Groups["asep"].Value)
                    {
                        if (node.GetAttribute("asep") == "")
                            node.SetAttribute("asep", m2.Groups["asep"].Value);
                        else
                            throw new System.InvalidOperationException("Line: (" + line + ") looks like a new question.");
                    }
 */
                }
                else
                    throw new System.InvalidOperationException("Line: (" + line + ") isn't a valid answers line.");

                /* Answer, dates, and text. */
                answernode = doc.CreateElement("ANSWER");
                node.AppendChild(answernode);
                adddates(answernode);
                answertextnode = doc.CreateElement("TEXT");
                answernode.AppendChild(answertextnode);
                answertextnode.AppendChild(doc.CreateTextNode(atext));

                /* How many answers do we have for this question? */
                allanswers = node.GetElementsByTagName("ANSWER");
                debugbox.Value += "Number of answers: " + allanswers.Count + "\n";

                qid = "q" + node.GetAttribute("qnum");
                aid = qid + "_a" + allanswers.Count;
                answernode.SetAttribute("id", aid);
                answernode.SetAttribute("position", "" + allanswers.Count);

                order_set_correct(aid, node);
                break;
        }
    }

    /* Simple class to sort a 2D array. */
    protected class JaggedComparer : IComparer
    {
        public int Compare(object x, object y)
        {
            // x and y are arrays of integers
            // sort on the 1st item in each array
            int[] a1 = (int[])x;
            int[] a2 = (int[])y;
            return (a1[0].CompareTo(a2[0]));
        }
    }

    protected void order_finisher(ref XmlElement node)
    {
        int count;
        int[][] array;
        XmlNodeList answerlist;
        XmlElement curnode;
        Random rand = new Random();
        IComparer cmp = new JaggedComparer();
        object[] nodearray;


        debugbox.Value += "In order finisher!\n";

        answerlist = node.GetElementsByTagName("ANSWER");
        count = answerlist.Count;

        array = new int[count][];
        nodearray = new object[count];

        for (int i = 0; i < count; i++)
        {
            array[i] = new int[2];

            array[i][0] = (int)rand.Next();
            array[i][1] = i;

            debugbox.Value += "Set entry[" + i + "] to pos: " + array[i][0] + "\n";

            /* Always pop off the first one. */
            curnode = (XmlElement)node.RemoveChild(answerlist[0]);
            nodearray[i] = (object)curnode;
        }

        for (int i = 0; i < count; i++)
        {
            debugbox.Value += "0: " + array[i][0] + " 1: " + array[i][0] + "\n";
        }

        Array.Sort(array, cmp);

        for (int i = 0; i < count; i++)
        {
            debugbox.Value += "Selected entry[" + array[i][1] + "] in pos: " + i + "\n";

            curnode = (XmlElement)nodearray[array[i][1]];
            curnode.SetAttribute("position", (i + 1).ToString());
            node.AppendChild(curnode);
        }
    }

    protected void order_set_correct(string aid, XmlElement node)
    {
        int order;
        XmlElement newnode;
        XmlElement gradable;
        XmlNodeList list;


        gradable = do_get_gradable(node);

        newnode = doc.CreateElement("CORRECTANSWER");
        gradable.AppendChild(newnode);
        newnode.SetAttribute("answer_id", aid);

        list = gradable.GetElementsByTagName("CORRECTANSWER");
        order = list.Count;

        newnode.SetAttribute("order", order.ToString());
    }

    protected void match_set_correct(string aid1, string aid2, XmlElement node)
    {
        XmlElement newnode;
        XmlElement gradable;
        XmlNodeList list;


        gradable = do_get_gradable(node);

        newnode = doc.CreateElement("CORRECTANSWER");
        gradable.AppendChild(newnode);
        newnode.SetAttribute("answer_id", aid1);
        newnode.SetAttribute("choice_id", aid2);
    }

    protected void match_rearrange(XmlElement node, string type)
    {
        int count;
        int[][] array;
        XmlNodeList nodelist;
        XmlElement curnode;
        Random rand;
        IComparer cmp = new JaggedComparer();
        object[] nodearray;


        Thread.Sleep(13);
        rand = new Random();

        debugbox.Value += "In match_rearrange\n";

        nodelist = node.GetElementsByTagName(type);
        count = nodelist.Count;

        array = new int[count][];
        nodearray = new object[count];

        for (int i = 0; i < count; i++)
        {
            array[i] = new int[2];

            array[i][0] = (int)rand.Next();
            array[i][1] = i;

            debugbox.Value += "Set entry[" + i + "] to pos: " + array[i][0] + "\n";

            /* Always pop off the first one. */
            curnode = (XmlElement)node.RemoveChild(nodelist[0]);
            nodearray[i] = (object)curnode;
        }

        for (int i = 0; i < count; i++)
        {
            debugbox.Value += "0: " + array[i][0] + " 1: " + array[i][1] + "\n";
        }

        Array.Sort(array, cmp);

        for (int i = 0; i < count; i++)
        {
            debugbox.Value += "Selected entry[" + array[i][1] + "] in pos: " + i + "\n";

            curnode = (XmlElement)nodearray[array[i][1]];
            curnode.SetAttribute("position", (i + 1).ToString());
            node.AppendChild(curnode);
        }
    }

    protected void match_finisher(ref XmlElement node)
    {
        debugbox.Value += "In order finisher!\n";

        match_rearrange(node, "ANSWER");
        match_rearrange(node, "CHOICE");
    }

    protected void match_handler(string line, ref XmlElement node)
    {
        string state = node.GetAttribute("state");


        debugbox.Value += "In match handler, state: " + state + " line: " + line + "\n";

        switch (state)
        {
            case "null":
                string qtext;
                XmlElement bodynode;
                XmlElement textnode;
                XmlElement flagsnode;
                XmlElement etcnode;
                XmlElement gradable;


                if (Regex.IsMatch(line, @"^\s*$"))
                    return;

                Regex firstline = new Regex(@"^\s*(?<qnum>\d+)[\)\.]\s*(?<qtext>.+)$");
                Match m = firstline.Match(line);
                /* Must have a nicely formatted first line. */
                if (m.Success)
                {
                    qtext = m.Groups["qtext"].Value.Trim();
					qtext = encode_smart_quotes(qtext);

                    debugbox.Value += "New question num: (" + m.Groups["qnum"].Value + ")\n";
                    debugbox.Value += "New question text: (" + qtext + ")\n";

                    numquestions++;

                    node.SetAttribute("qnum", numquestions.ToString());
                    node.SetAttribute("qtext", qtext);
                }
                else
                    throw new System.InvalidOperationException("Line: (" + line + ") isn't a valid first line.");

                node.SetAttribute("qtype", "QUESTION_MATCH");

                adddates(node);

                /* Body tag and text. */
                bodynode = doc.CreateElement("BODY");
                node.AppendChild(bodynode);
                textnode = doc.CreateElement("TEXT");
                bodynode.AppendChild(textnode);
                textnode.AppendChild(doc.CreateTextNode(qtext));

                /* Flags subtree. */
                flagsnode = doc.CreateElement("FLAGS");
                bodynode.AppendChild(flagsnode);
                etcnode = doc.CreateElement("ISHTML");
                flagsnode.AppendChild(etcnode);
                etcnode.SetAttribute("value", "true");
                etcnode = doc.CreateElement("ISNEWLINELITERAL");
                flagsnode.AppendChild(etcnode);
                flagsnode.SetAttribute("value", "true");

                /* Next step... */
                node.SetAttribute("state", "answers");
                break;

            case "answers":
                string aid = "";
                string cid = "";
                string qid;
                string atext;
                string ctext;
                XmlElement answernode;
                XmlElement answertextnode;
                XmlElement choicenode;
                XmlElement choicetextnode;
                XmlNodeList allanswers;
                XmlNodeList allchoices;


                /* Empty line means new question. */
                if (Regex.IsMatch(line, @"^\s*$"))
                    throw new System.InvalidOperationException("Empty line in answers state means new question.");

                Regex answerline = new Regex(@"^\s*(?<anum>(\d+|\w))(?<asep>[\)\.])\s*(?<atext>.*)?\s*\/\s*(?<ctext>.*)$");
                Match m2 = answerline.Match(line);

                /* Must have a nicely formatted answer line. */
                if (m2.Success)
                {
                    atext = m2.Groups["atext"].Value.Trim();
					atext = encode_smart_quotes(atext);
                    ctext = m2.Groups["ctext"].Value.Trim();

                    debugbox.Value += "New answer num: (" + m2.Groups["anum"].Value + ")\n";
                    debugbox.Value += "New answer text: (" + atext + ")\n";
                    debugbox.Value += "New choice text: (" + ctext + ")\n";
                    debugbox.Value += "Answer separator: (" + m2.Groups["asep"].Value + ")\n";

                    /* Kind of difficult to distinguish between question and answer lines. */
                    /* This assumes they have different separators. */
/*
                    if (node.GetAttribute("asep") != m2.Groups["asep"].Value)
                    {
                        if (node.GetAttribute("asep") == "")
                            node.SetAttribute("asep", m2.Groups["asep"].Value);
                        else
                            throw new System.InvalidOperationException("Line: (" + line + ") looks like a new question.");
                    }
 */
                }
                else
                    throw new System.InvalidOperationException("Line: (" + line + ") isn't a valid answers line.");

                /* How many answers do we have for this question? */
                /* Note, this object is eerily always right, regardless of how many
                    * nodes are in the list when you create it.  I don't like it. */
                allanswers = node.GetElementsByTagName("ANSWER");
                allchoices = node.GetElementsByTagName("CHOICE");
                debugbox.Value += "Number of answers: " + allanswers.Count + "\n";
                debugbox.Value += "Number of choices: " + allchoices.Count + "\n";
                qid = "q" + node.GetAttribute("qnum");

                if (atext.Length > 0)
                {
                    /* Left hand answer, dates, and text. */
                    answernode = doc.CreateElement("ANSWER");
                    node.AppendChild(answernode);
                    adddates(answernode);
                    answertextnode = doc.CreateElement("TEXT");
                    answernode.AppendChild(answertextnode);
                    answertextnode.AppendChild(doc.CreateTextNode(atext));

                    aid = qid + "_a" + allanswers.Count;
                    answernode.SetAttribute("id", aid);
                    answernode.SetAttribute("placement", "left");
                }

                if (ctext.Trim().Length > 1)
                {
                    /* Right hand answer, dates, and text. */
                    choicenode = doc.CreateElement("CHOICE");
                    node.AppendChild(choicenode);
                    adddates(choicenode);
                    choicetextnode = doc.CreateElement("TEXT");
                    choicenode.AppendChild(choicetextnode);
                    choicetextnode.AppendChild(doc.CreateTextNode(ctext));

                    cid = qid + "_c" + allchoices.Count;
                    choicenode.SetAttribute("id", cid);
                    choicenode.SetAttribute("placement", "right");
                }

                if (!(aid.Equals("") || cid.Equals("")))
                    match_set_correct(aid, cid, node);

                break;
        }
    }

    protected void nyi_handler(string line, ref XmlElement node)
    {
        debugbox.Value += "In NYI handler\n";
        throw new System.InvalidOperationException("NYI handler!");
    }

}