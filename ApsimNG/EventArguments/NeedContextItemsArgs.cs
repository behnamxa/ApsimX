﻿// -----------------------------------------------------------------------
// <copyright file="NeedContextItemsArgs.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------

namespace UserInterface.EventArguments
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Models.Core;
    using System.Text;
    using APSIM.Shared.Utilities;
    using Intellisense;
    using System.Xml;

    /// <summary>
    /// The editor view asks the presenter for context items. This structure
    /// is used to do that
    /// </summary>
    public class NeedContextItemsArgs : EventArgs
    {
        /// <summary>
        /// The name of the object that needs context items.
        /// </summary>
        public string ObjectName;

        /// <summary>
        /// The items returned from the presenter back to the view
        /// </summary>
        public List<ContextItem> AllItems;

        /// <summary>
        /// Context item information
        /// </summary>
        public List<string> Items;

        /// <summary>
        /// Completion data.
        /// </summary>
        public List<CompletionData> CompletionData { get; set; }

        /// <summary>
        /// Co-ordinates at which the intellisense window should be displayed.
        /// </summary>
        public Tuple<int, int> Coordinates { get; set; }

        /// <summary>
        /// Source code for which we need completion options.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Offset of the caret in the source code.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// True iff this intellisense request was generated by the user pressing control space.
        /// </summary>
        public bool ControlSpace { get; set; }

        /// <summary>
        /// The line that the caret is on.
        /// </summary>
        public int LineNo { get; set; }

        /// <summary>
        /// The column that the caret is on.
        /// </summary>
        public int ColNo { get; set; }

        /// <summary>
        /// The view is asking for variable names for its intellisense.
        /// </summary>
        /// <param name="atype">Data type for which we want completion options.</param>
        /// <param name="properties">If true, property suggestions will be generated.</param>
        /// <param name="methods">If true, method suggestions will be generated.</param>
        /// <param name="events">If true, event suggestions will be generated.</param>
        /// <returns>List of completion options.</returns>
        public static List<ContextItem> ExamineTypeForContextItems(Type atype, bool properties, bool methods, bool events)
        {
            List<ContextItem> allItems = new List<ContextItem>();

            // find the properties and methods
            if (atype != null)
            {
                if (properties)
                {
                    foreach (PropertyInfo property in atype.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        VariableProperty var = new VariableProperty(atype, property);
                        ContextItem item = new ContextItem
                        {
                            Name = var.Name,
                            IsProperty = true,
                            IsEvent = false,
                            IsWriteable = !var.IsReadOnly,
                            TypeName = var.DataType.Name,
                            Descr = GetDescription(property),
                            Units = var.Units
                        };
                        allItems.Add(item);
                    }
                }

                if (methods)
                {
                    foreach (MethodInfo method in atype.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (!method.Name.StartsWith("get_") && !method.Name.StartsWith("set_"))
                        {
                            ContextItem item = new ContextItem
                            {
                                Name = method.Name,
                                IsProperty = false,
                                IsEvent = false,
                                IsMethod = true,
                                IsWriteable = false,
                                TypeName = method.ReturnType.Name,
                                Descr = GetDescription(method),
                                Units = string.Empty
                            };

                            // build a parameter string representation
                            ParameterInfo[] allparams = method.GetParameters();
                            StringBuilder paramText = new StringBuilder("( ");
                            if (allparams.Count() > 0)
                            {
                                for (int p = 0; p < allparams.Count(); p++)
                                {
                                    ParameterInfo parameter = allparams[p];
                                    paramText.Append(parameter.ParameterType.Name + " " + parameter.Name);
                                    if (parameter.DefaultValue != DBNull.Value)
                                        paramText.Append(" = " + parameter.DefaultValue.ToString());
                                    if (p < allparams.Count() - 1)
                                        paramText.Append(", ");
                                }
                            }
                            paramText.Append(" )");
                            item.ParamString = paramText.ToString();

                            allItems.Add(item);
                        }
                    }
                }

                if (events)
                {
                    foreach (EventInfo evnt in atype.GetEvents(BindingFlags.Instance | BindingFlags.Public))
                    {
                        NeedContextItemsArgs.ContextItem item = new NeedContextItemsArgs.ContextItem();
                        item.Name = evnt.Name;
                        item.IsProperty = true;
                        item.IsEvent = true;
                        item.IsMethod = false;
                        item.IsWriteable = false;
                        item.TypeName = evnt.ReflectedType.Name;
                        item.Descr = GetDescription(evnt);
                        item.Units = "";
                        allItems.Add(item);
                    }
                }
            }

            allItems.Sort(delegate(ContextItem c1, ContextItem c2) { return c1.Name.CompareTo(c2.Name); });
            return allItems;
        }

        /// <summary>
        /// The view is asking for variable names for its intellisense.
        /// </summary>
        /// <param name="o">Fully- or partially-qualified object name for which we want completion options.</param>
        /// <param name="properties">If true, property suggestions will be generated.</param>
        /// <param name="methods">If true, method suggestions will be generated.</param>
        /// <param name="events">If true, event suggestions will be generated.</param>
        /// <returns>List of completion options.</returns>
        private static List<ContextItem> ExamineObjectForContextItems(object o, bool properties, bool methods, bool events)
        {
            List<ContextItem> allItems;
            Type objectType = o is Type ? o as Type : o.GetType();
            allItems = ExamineTypeForContextItems(objectType, properties, methods, events);
            
            // add in the child models.
            if (o is IModel)
            {
                foreach (IModel model in (o as IModel).Children)
                {
                    if (allItems.Find(m => m.Name == model.Name) == null)
                    {
                        NeedContextItemsArgs.ContextItem item = new NeedContextItemsArgs.ContextItem();
                        item.Name = model.Name;
                        item.IsProperty = false;
                        item.IsEvent = false;
                        item.IsWriteable = false;
                        item.TypeName = model.GetType().Name;
                        item.Units = string.Empty;
                        allItems.Add(item);
                    }
                }
                allItems.Sort(delegate(ContextItem c1, ContextItem c2) { return c1.Name.CompareTo(c2.Name); });
            }
            return allItems;
        }

        /// <summary>
        /// The view is asking for variable names.
        /// </summary>
        /// <param name="relativeTo">Model in the simulations tree which owns the editor.</param>
        /// <param name="objectName">Fully- or partially-qualified object name for which we want completion options.</param>
        /// <param name="properties">If true, property suggestions will be generated.</param>
        /// <param name="methods">If true, method suggestions will be generated.</param>
        /// <param name="events">If true, event suggestions will be generated.</param>
        /// <returns>List of completion options.</returns>
        public static List<ContextItem> ExamineModelForNames(IModel relativeTo, string objectName, bool properties, bool methods, bool events)
        {
            // TODO : refactor cultivar and report activity ledger presenters so they use the intellisense presenter. 
            // These are the only two presenters which still use this intellisense method.
            if (objectName == string.Empty)
                objectName = ".";

            object o = null;
            IModel replacementModel = Apsim.Get(relativeTo, ".Simulations.Replacements") as IModel;
            if (replacementModel != null)
            {
                try
                {
                    o = Apsim.Get(replacementModel, objectName) as IModel;
                }
                catch (Exception) {  }
            }

            if (o == null)
            {
                try
                {
                    o = Apsim.Get(relativeTo, objectName);
                }
                catch (Exception) { }
            }
            
            if (o == null && relativeTo.Parent is Replacements)
            {
                // Model 'relativeTo' could be under replacements. Look for the first simulation and try that.
                IModel simulation = Apsim.Find(relativeTo.Parent.Parent, typeof(Simulation));
                try
                {
                    o = Apsim.Get(simulation, objectName) as IModel;
                }
                catch (Exception) { }
            }

            if (o != null)
            {
                return ExamineObjectForContextItems(o, properties, methods, events);
            }

            return new List<ContextItem>();
        }

        /// <summary>
        /// Generates a list of context items for given model.
        /// Uses <see cref="GetNodeFromPath(Model, string)"/> to get the model reference.
        /// </summary>
        /// <param name="relativeTo">Model that the string is relative to.</param>
        /// <param name="objectName">Name of the model that we want context items for.</param>
        /// <returns></returns>
        public static List<ContextItem> ExamineModelForContextItemsV2(Model relativeTo, string objectName, bool properties, bool methods, bool events)
        {
            List<ContextItem> contextItems = new List<ContextItem>();
            object node = GetNodeFromPath(relativeTo, objectName);
            if (node != null)
            {
                contextItems = ExamineObjectForContextItems(node, properties, methods, events);
            }
            return contextItems;
        }

        /// <summary>
        /// A new method for finding a model/object from a path in the simulations tree.
        /// Finds the node (whose name is surrounded by square brackets). From there, it looks for each
        /// successive period-delimited child or property given in the path string.
        /// </summary>
        /// <param name="relativeTo">Object in the simulations tree.</param>
        /// <param name="objectName">Name of the object or model for which we want completion options.</param>
        /// <returns></returns>
        private static object GetNodeFromPath(Model relativeTo, string objectName)
        {       
            string modelNamePattern = @"\[[A-Za-z\s]+[A-Za-z0-9\s_]*\]";
            var matches = System.Text.RegularExpressions.Regex.Matches(objectName, modelNamePattern);
            if (matches.Count <= 0)
                return null;

            // Get the raw model name without square brackets.
            string modelName = matches[0].Value.Replace("[", "").Replace("]", "");

            // Get the node in the simulations tree corresponding to the model name which was surrounded by square brackets.
            object node = Apsim.ChildrenRecursively(Apsim.Parent(relativeTo, typeof(Simulations))).FirstOrDefault(child => child.Name == modelName);

            // If the object name string does not contain any children/properties 
            // (e.g. it doesn't contain any periods), we can return immediately.
            if (!objectName.Contains("."))
                return node;

            objectName = objectName.Substring(objectName.IndexOf('.') + 1);

            // Iterate over the 'child' models/properties.
            // childName is the next child we're looking for. e.g. in "[Wheat].Leaf", the first childName will be "Leaf".
            string[] namePathBits = APSIM.Shared.Utilities.StringUtilities.SplitStringHonouringBrackets(objectName, '.', '[', ']');
            for (int i = 0; i < namePathBits.Length; i++)
            {
                if (node == null)
                    return null;
                string childName = namePathBits[i];

                int squareBracketIndex = childName.IndexOf('[');
                if (squareBracketIndex == 0)
                {
                    // User has typed something like [Wheat].[...]
                    throw new Exception("Unable to parse child or property " + childName);
                }
                if (squareBracketIndex > 0) // childName contains square brackets - it may be an IList element
                    childName = childName.Substring(0, squareBracketIndex);
                
                // First, check the child models. 
                if (node is IModel)
                    node = (node as IModel).Children.FirstOrDefault(c => c.Name == childName) ?? node;

                // If we couldn't find a matching child model, we check the model/object's properties.

                // This expression evaluates to true if node is not an IModel.
                if ((node as IModel)?.Name != childName)
                {
                    // Node cannot be null here.
                    try
                    {
                        Type propertyType = node is Type ? node as Type : node.GetType();
                        PropertyInfo property = propertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(p => p.Name == childName);

                        // If we couldn't find any matching child models or properties, all we can do is return.
                        if (property == null)
                            return null;

                        // Try to set node to the value of the property.
                        node = ReflectionUtilities.GetValueOfFieldOrProperty(childName, node);
                        if (node == null)
                        {
                            // This property has the correct name. If the property's type provides a parameterless constructor, we can use 
                            // reflection to instantiate an object of that type and assign it to the node variable. 
                            // Otherwise, we assign the type itself to node.
                            if (property.PropertyType.GetConstructor(Type.EmptyTypes) == null)
                                node = property.PropertyType;
                            else
                                node = Activator.CreateInstance(property.PropertyType);
                        }
                    }
                    catch
                    {
                        // Any 'real' errors should be displayed to the user, so they should be caught 
                        // in a presenter which can access the explorer presenter.
                        // Because of this, any unhandled exceptions here will kill the intellisense 
                        // generation operation, and we still have a few tricks up our sleeve.
                        return null;
                    }
                }

                if (squareBracketIndex > 0)
                {
                    // We have found the node, but the node is an IList of some sort, and we are actually interested in a specific element.

                    int closingBracketIndex = namePathBits[i].IndexOf(']');
                    if (closingBracketIndex <= 0 || (closingBracketIndex - squareBracketIndex) < 1)
                        return null;

                    string textBetweenBrackets = namePathBits[i].Substring(squareBracketIndex + 1, closingBracketIndex - squareBracketIndex - 1);
                    if (node is IList)
                    {
                        int index = -1;
                        if (Int32.TryParse(textBetweenBrackets, out index))
                        {
                            IList nodeList = node as IList;
                            if (index > nodeList.Count || index <= 0)
                                node = node.GetType().GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>)).Select(x => x.GetGenericArguments()[0]).FirstOrDefault();
                            else
                                node = nodeList[index - 1];
                        }
                        else
                            throw new Exception("Unable to access element \"" + textBetweenBrackets + "\" of list \"" + namePathBits[i] + "\"");
                    }
                    else if (node is IDictionary)
                    {
                        node = (node as IDictionary)[textBetweenBrackets];
                    }
                    else
                        throw new Exception("Unable to parse child or property name " + namePathBits[i]);
                }
                squareBracketIndex = -1;
            }
             return node;
        }

        /// <summary>
        /// Gets the contents of a property's summary tag, or, if the summary tag doesn't exist,
        /// a <see cref="DescriptionAttribute"/>.
        /// </summary>
        /// <param name="member">Property whose documentation will be retrieved.</param>
        /// <returns>
        /// Contents of a summary tag (if available), or a description attribute,
        /// or an empty string if neither of these are available.
        /// </returns>
        private static string GetDescription(MemberInfo member)
        {
            if (member == null)
                return string.Empty;

            // The member's documentation doesn't reside in the compiled assembly - it's actually stored in
            // an xml documentation file which usually sits next to the assembly on disk.
            string documentationFile = System.IO.Path.ChangeExtension(member.Module.Assembly.Location, "xml");
            if (!System.IO.File.Exists(documentationFile))
            {
                // If the documentation file doesn't exist, this member is probably a member of a manager script.
                // These members usually have a description attribute which can be used instead.
                Attribute description = member.GetCustomAttribute(typeof(DescriptionAttribute));
                // If the property has no description attribute, just return an empty string.
                return description == null ? string.Empty : description.ToString();
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(documentationFile);
            string memberPrefix = string.Empty;
            if (member is PropertyInfo)
                memberPrefix = "P";
            else if (member is FieldInfo)
                // This shouldn't be run for public fields, but it doesn't hurt to be thorough.
                memberPrefix = "F";
            else if (member is MethodInfo)
                memberPrefix = "M";
            else if (member is EventInfo)
                memberPrefix = "E";

            string xPath = string.Format("//member[starts-with(@name, '{0}:{1}.{2}')]/summary[1]", memberPrefix, member.DeclaringType.FullName, member.Name);
            XmlNode summaryNode = doc.SelectSingleNode(xPath);
            if (summaryNode == null || summaryNode.ChildNodes.Count < 1)
                return string.Empty;
            // The summary tag often spans multiple lines, which means that the text inside usually
            // starts and ends with newlines (and indentation), which we don't want to display.
            return summaryNode.InnerText.Trim(Environment.NewLine.ToCharArray()).Trim();
        }

        /// <summary>
        /// Complete context item information
        /// </summary>
        public class ContextItem
        {
            /// <summary>
            /// Name of the item
            /// </summary>
            public string Name;

            /// <summary>
            /// The return type as a string
            /// </summary>
            public string TypeName;

            /// <summary>
            /// Units string
            /// </summary>
            public string Units;

            /// <summary>
            /// The description string
            /// </summary>
            public string Descr;

            /// <summary>
            /// This is an event.
            /// </summary>
            public bool IsEvent;

            /// <summary>
            /// This is a method.
            /// </summary>
            public bool IsMethod;

            /// <summary>
            /// String that represents the parameter list
            /// </summary>
            public string ParamString;

            /// <summary>
            /// This is a property
            /// </summary>
            public bool IsProperty;

            /// <summary>
            /// This property is writeable
            /// </summary>
            public bool IsWriteable;

            /// <summary>
            /// The property is a child model.
            /// </summary>
            public bool IsChildModel;
        }
    } 
}
