using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime
{
	public class XMLTag
  {
    public string Name = "";
    public Dictionary<string, string> Attributes = new Dictionary<string, string>();
    public string Content = "";

    public string this[string key]
    {
      get
      {
        if (Attributes.ContainsKey(key))
          return Attributes[key];
        return "";
      }
    }
  }

  public class SimpleXMLReader
  {
    public static XMLTag Parse(string xml)
    {
      XMLTag ret = new XMLTag();

      // Reading states
      bool readingTag = false;
      bool readingTagName = false;
      bool readingAttributeName = false;
      bool readingAttribute = false;
      bool readingContent = false;

      // Temporary content
      string currentTagName = "";
      string currentAttributeName = "";
      string currentAttribute = "";
      string currentContent = "";

      // Loop through all the characters
      for (int i = 0; i < xml.Length; i++) {
        char c = xml[i];

        // Start of tag or start of end tag
        if (c == '<') {
          if (!readingTag) {
            // Start of tag
            readingTag = true;
            readingTagName = true;
            continue;
          } else if (xml[++i] == '/') {
            // Start of end tag, so we can stop reading here
            ret.Content = currentContent;
            break;
          }
        }

        // End of tag
        if (c == '>') {
          if (xml[i - 1] == '/') {
            // There's no more content
            readingTag = false;
          } else {
            // Read inner tag content
            readingContent = true;
          }
          continue;
        }

        // Seperator in between attributes and tag name
        if (c == ' ') {
          if (readingTagName) {
            // Set the tag name
            readingTagName = false;
            ret.Name = currentTagName;
          }

          // Regarding attributes
          if (readingTag && !readingContent && !readingAttribute) {
            readingAttributeName = true;
            continue;
          }
        }

        // Definition character
        if (c == '=') {
          if (readingTag && readingAttributeName) {
            readingAttributeName = false;
            continue;
          }
        }

        // String
        if (c == '"') {
          if (!readingContent) {
            if (!readingAttribute) {
              // Start reading
              readingAttribute = true;
              continue;
            } else {
              // Stop reading
              readingAttribute = false;

              // Add to attribute list
              ret.Attributes.Add(currentAttributeName, currentAttribute);

              // Clear out
              currentAttributeName = "";
              currentAttribute = "";
              continue;
            }
          }
        }

        // Add character to string
        if (readingTag) {
          // Tag name
          if (readingTagName) {
            currentTagName += c;
            continue;
          }

          // Attribute value
          if (readingAttribute) {
            currentAttribute += c;
            continue;
          }

          // Attribute name
          if (readingAttributeName) {
            currentAttributeName += c;
            continue;
          }

          // Content
          if (readingContent) {
            currentContent += c;
            continue;
          }
        }
      }

      return ret;
    }
  }
}
