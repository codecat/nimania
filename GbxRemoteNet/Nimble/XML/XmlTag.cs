using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Nimble.Extensions;
using Nimble.Utils;
using System.Net;

namespace Nimble.XML
{
  public class XmlTag
  {
    public string Name { get; set; }
    public string Value { get; set; }
    public bool IsComment { get; set; }
    public bool IsTextNode { get; set; }

    public XmlTag Parent { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
    public List<XmlTag> Children { get; set; }

    public XmlTag(XmlTag parent)
    {
      Name = "";
      Value = "";
      IsComment = false;
      IsTextNode = false;

      Parent = parent;
      Attributes = new Dictionary<string, string>();
      Children = new List<XmlTag>();
    }

    public override string ToString()
    {
      string strAttributes = "";
      foreach (string attr in Attributes.Keys) {
        strAttributes += " " + attr + "=\"" + ExternalEncoding.EncodeHtml(Attributes[attr]) + "\"";
      }
      return "<" + Name + strAttributes + " />";
    }

    internal void Parse(StreamReader fs)
    {
      bool bReadAttributes = true;
      bool bOpenTag = false;

      while (true) {
        if (fs.PeekChar() == '?') {
          // xml decleration is a small exception
          fs.Expect("?xml ");
          bReadAttributes = true;
          break;
        } else if (fs.PeekChar() == '!') {
          // xml comment
          fs.Expect("!--");
          IsComment = true;
          var strValue = new StringBuilder();
          string strEnding = "-->";
          int i = 0;
          while (!fs.EndOfStream) {
            char c = fs.ReadChar();
            if (c == strEnding[i]) {
              i++;
            } else {
              i = 0;
              strValue.Append(c);
            }
            if (i == strEnding.Length) {
              break;
            }
          }
          Value = WebUtility.HtmlDecode(strValue.ToString());
          return;
        } else {
          // xml tag
          string strName = "";
          // read the name of the tag, and get the character we ended with
          char c = fs.ReadUntil(out strName, '\r', '\n', '\t', ' ', '>', '/');
          Name = strName;
          if (c == '/' && fs.PeekChar() == '>') {
            // if it's "/>", it's a closed tag
            bReadAttributes = false;
            bOpenTag = false;
            fs.Expect('>');
          } else if (c == '>') {
            // if it's a end-of-tag character, it's an open tag 
            bReadAttributes = false;
            bOpenTag = true;
          }
          break;
        }
      }

      // read attributes
      if (bReadAttributes) {
        var attrs = XmlHelpers.ParseAttributes(fs);
        Attributes = attrs.attributes;
        bOpenTag = attrs.bOpenTag;
      }

      // if open tag
      if (bOpenTag) {
        // start reading the value
        var strValue = new StringBuilder();
        var strValueTextNodes = new StringBuilder();

        // start reading text nodes
        var tagTextNode = new XmlTag(this);
        tagTextNode.IsTextNode = true;

        while (!fs.EndOfStream) {
          char c = fs.ReadChar();
          // check for tag
          if (c == '<') {
            // if there's useful content in the text node buffer
            if (strValueTextNodes.Length > 0) {
              string strTextNodeContent = strValueTextNodes.ToString().Trim(new[] { '\r', '\n', '\t' });
              if (strTextNodeContent.Length > 0) {
                // add the text node to the children
                tagTextNode.Value = WebUtility.HtmlDecode(strTextNodeContent);
                strValueTextNodes.Clear();
                Children.Add(tagTextNode);
                // start a new text node
                tagTextNode = new XmlTag(this);
                tagTextNode.IsTextNode = true;
              }
            }
            // if this is the end of the tag
            if (fs.PeekChar() == '/') {
              // break out
              fs.Expect("/" + Name + ">");
              break;
            } else {
              // new tag nested in this tag
              XmlTag newTag = new XmlTag(this);
              newTag.Parse(fs);
              Children.Add(newTag);
            }
          } else {
            // add to value
            strValue.Append(c);
            // add to text note buffer
            strValueTextNodes.Append(c);
          }
        }

        // set value property
        Value = WebUtility.HtmlDecode(strValue.ToString());
        strValue.Clear();

        // if there's useful content in the text node buffer
        if (strValueTextNodes.Length > 0) {
          string strTextNodeContent = strValueTextNodes.ToString().Trim(new[] { '\r', '\n', '\t' });
          if (strTextNodeContent.Length > 0) {
            // add the text node to the children
            tagTextNode.Value = WebUtility.HtmlDecode(strValueTextNodes.ToString());
            strValueTextNodes.Clear();
            Children.Add(tagTextNode);
          }
        }
      }
    }

    public XmlTag FindTagByName(string strName)
    {
      foreach (XmlTag tag in Children) {
        if (tag.Name == strName) {
          return tag;
        }
      }

      foreach (XmlTag tag in Children) {
        if (tag.Children.Count == 0) {
          continue;
        }

        var ret = tag.FindTagByName(strName);
        if (ret != null) {
          return ret;
        }
      }

      return null;
    }

    public XmlTag[] FindTagsByName(string strName)
    {
      var arr = new List<XmlTag>();

      foreach (XmlTag tag in Children) {
        if (tag.Name == strName) {
          arr.Add(tag);
        }
      }

      foreach (XmlTag tag in Children) {
        if (tag.Children.Count == 0) {
          continue;
        }

        arr.AddRange(tag.FindTagsByName(strName));
      }

      return arr.ToArray();
    }

    public XmlTag FindTagByNameAndAttribute(string strName, string strAttrib, string strValue)
    {
      XmlTag[] tags = FindTagsByName(strName);

      foreach (XmlTag tag in tags) {
        if (tag.Attributes.ContainsKey(strAttrib) && tag.Attributes[strAttrib] == strValue) {
          return tag;
        }
      }

      return null;
    }

    public XmlTag FindTagByAttribute(string strAttrib, string strValue)
    {
      foreach (XmlTag tag in Children) {
        if (tag.Attributes.ContainsKey(strAttrib) && tag.Attributes[strAttrib] == strValue) {
          return tag;
        }
      }

      foreach (XmlTag tag in Children) {
        if (tag.Children.Count == 0) {
          continue;
        }

        var ret = tag.FindTagByAttribute(strAttrib, strValue);
        if (ret != null) {
          return ret;
        }
      }

      return null;
    }

    public XmlTag[] FindTagsByAttribute(string strAttrib, string strValue)
    {
      var arr = new List<XmlTag>();

      foreach (XmlTag tag in Children) {
        if (tag.Attributes.ContainsKey(strAttrib) && tag.Attributes[strAttrib] == strValue) {
          arr.Add(tag);
        }
      }

      foreach (XmlTag tag in Children) {
        if (tag.Children.Count == 0) {
          continue;
        }

        arr.AddRange(tag.FindTagsByAttribute(strAttrib, strValue));
      }

      return arr.ToArray();
    }

    public XmlTag this[string strQuery]
    {
      get
      {
        // "test[a=b]"
        string strAttribRegex = @"^([^\[]+)(\[([^=]+)=([^\]]*)\])?$";
        Match match = Regex.Match(strQuery, strAttribRegex);
        Debug.Assert(match.Success);

        string strName = match.Groups[1].Value;
        string strAttrib = match.Groups[3].Value;
        string strValue = match.Groups[4].Value;

        if (strAttrib != "") {
          return FindTagByNameAndAttribute(strName, strAttrib, strValue);
        } else {
          return FindTagByName(strQuery);
        }
      }
    }
  }
}
