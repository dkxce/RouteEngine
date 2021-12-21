using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.Services;
using System.Web.Services.Description;
using System.Xml;

namespace dkxce.Route.ServiceSolver
{
    public class WSDL
    {
        public static string GenerateWSDL(Type type)
        {
            return GenerateWSDL(type, false);
        }

        public static string GenerateWSDL(Type type, bool withXSLT)
        {
            string URL = "http://localhost/SOAP";

            object[] attrs = type.GetCustomAttributes(false);
            foreach (object attr in attrs)
                if (attr is WebServiceAttribute)
                {
                    WebServiceAttribute wsa = (WebServiceAttribute)attr;
                    if (!string.IsNullOrEmpty(wsa.Namespace)) URL = wsa.Namespace;
               };

            return GenerateWSDL(type, URL, withXSLT);
        }

        public static string GenerateWSDL(Type type, string URL)
        {
            return GenerateWSDL(type, URL, false);
        }

        public static string GenerateWSDL(Type type, string URL, bool withXSLT)
        {
            ServiceDescriptionReflector reflector = new ServiceDescriptionReflector();
            reflector.Reflect(type, URL);

            if (reflector.ServiceDescriptions.Count > 1)
                throw new Exception("Multiple service descriptions not supported.");

            MemoryStream stream = new MemoryStream();
            System.Xml.XmlWriterSettings xws = new XmlWriterSettings();
            xws.OmitXmlDeclaration = true;
            xws.Indent = true;
            xws.Encoding = System.Text.Encoding.UTF8;
            XmlWriter xmlWriter = XmlWriter.Create(stream, xws);
            reflector.ServiceDescriptions[0].Write(xmlWriter);

            StreamReader textReader = new StreamReader(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" + 
                (withXSLT ? "<?xml-stylesheet type=\"text/xsl\" href=\"help/xmlwsdl.xslt\"?>\r\n" : "") +
                textReader.ReadToEnd();
        }
    }
}
