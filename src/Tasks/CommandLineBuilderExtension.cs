﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Globalization;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// CommandLineBuilder derived class for specialized logic specific to MSBuild tasks
    /// </summary>
    public class CommandLineBuilderExtension : CommandLineBuilder
    {
        /// <summary>
        /// Initializes a new instance of the CommandLineBuilderExtension class.
        /// </summary>
        public CommandLineBuilderExtension()
        {
        }

        /// <summary>
        /// Initializes a new instance of the CommandLineBuilderExtension class.
        /// </summary>
        public CommandLineBuilderExtension(bool quoteHyphensOnCommandLine, bool useNewLineSeparator)
            : base(quoteHyphensOnCommandLine, useNewLineSeparator)
        {
        }

        /// <summary>
        /// Set a boolean switch iff its value exists and its value is 'true'.
        /// </summary>
        internal void AppendWhenTrue
            (
            string switchName,
            Hashtable bag,
            string parameterName
            )
        {
            object obj = bag[parameterName];
            // If the switch isn't set, don't add it to the command line.
            if (obj != null)
            {
                bool value = (bool)obj;

                if (value)
                {
                    AppendSwitch(switchName);
                }
            }
        }

        /// <summary>
        /// Set a boolean switch only if its value exists.
        /// </summary>
        internal void AppendPlusOrMinusSwitch
            (
            string switchName,
            Hashtable bag,
            string parameterName
            )
        {
            object obj = bag[parameterName];
            // If the switch isn't set, don't add it to the command line.
            if (obj != null)
            {
                bool value = (bool)obj;
                // Do not quote - or + as they are part of the switch
                AppendSwitchUnquotedIfNotNull(switchName, value ? "+" : "-");
            }
        }

        /// <summary>
        /// Set a boolean switch only if its value exists.
        /// </summary>
        internal void AppendPlusOrMinusSwitch
            (
            string switchName,
            bool? value
            )
        {
            if (value.HasValue)
            {
                AppendSwitchUnquotedIfNotNull(switchName, value.Value ? "+" : "-");
            }
        }


        /// <summary>
        /// Set a switch if its value exists by choosing from the input choices
        /// </summary>
        internal void AppendByChoiceSwitch
            (
            string switchName,
            Hashtable bag,
            string parameterName,
            string choice1,
            string choice2
            )
        {
            object obj = bag[parameterName];
            // If the switch isn't set, don't add it to the command line.
            if (obj != null)
            {
                bool value = (bool)obj;
                AppendSwitchUnquotedIfNotNull(switchName, value ? choice1 : choice2);
            }
        }

        /// <summary>
        /// Set an integer switch only if its value exists.
        /// </summary>
        internal void AppendSwitchWithInteger
            (
            string switchName,
            Hashtable bag,
            string parameterName
            )
        {
            object obj = bag[parameterName];
            // If the switch isn't set, don't add it to the command line.
            if (obj != null)
            {
                int value = (int)obj;
                AppendSwitchIfNotNull(switchName, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Adds an aliased switch, used for ResGen:
        ///      /reference:Foo=System.Xml.dll
        /// </summary>
        internal void AppendSwitchAliased(string switchName, string alias, string parameter)
        {
            AppendSwitchUnquotedIfNotNull(switchName, alias + "=");
            AppendTextWithQuoting(parameter);
        }

        /// <summary>
        /// Adds a nested switch, used by SGen.exe.  For example:
        ///     /compiler:"/keyfile:\"c:\some folder\myfile.snk\""
        /// </summary>
        /// <param name="outerSwitchName"></param>
        /// <param name="innerSwitchName"></param>
        /// <param name="parameter"></param>
        internal void AppendNestedSwitch(string outerSwitchName, string innerSwitchName, string parameter)
        {
            string quotedParameter = GetQuotedText(parameter);
            AppendSwitchIfNotNull(outerSwitchName, innerSwitchName + quotedParameter);
        }

        /// <summary>
        /// Returns a quoted string appropriate for appending to a command line.
        /// </summary>
        /// <remarks>
        /// Escapes any double quotes in the string.
        /// </remarks>
        /// <param name="unquotedText"></param>
        protected string GetQuotedText(string unquotedText)
        {
            StringBuilder quotedText = new StringBuilder();

            AppendQuotedTextToBuffer(quotedText, unquotedText);

            return quotedText.ToString();
        }

        /// <summary>
        /// Appends a command-line switch that takes a compound string parameter. The parameter is built up from the item-spec and
        /// the specified attributes. The switch is appended as many times as there are parameters given.
        /// </summary>
        /// <param name="switchName"></param>
        /// <param name="parameters"></param>
        /// <param name="attributes"></param>
        internal void AppendSwitchIfNotNull
        (
            string switchName,
            ITaskItem[] parameters,
            string[] attributes
        )
        {
            AppendSwitchIfNotNull(switchName, parameters, attributes, null /* treatAsFlag */);
        }

        /// <summary>
        /// Append a switch if 'parameter' is not null.
        /// Split on the characters provided.
        /// </summary>
        internal void AppendSwitchWithSplitting(string switchName, string parameter, string delimiter, params char[] splitOn)
        {
            if (parameter != null)
            {
                string[] splits = parameter.Split(splitOn, /* omitEmptyEntries */ StringSplitOptions.RemoveEmptyEntries);
                string[] splitAndTrimmed = new string[splits.Length];
                for (int i = 0; i < splits.Length; ++i)
                {
                    splitAndTrimmed[i] = splits[i].Trim();
                }
                AppendSwitchIfNotNull(switchName, splitAndTrimmed, delimiter);
            }
        }

        /// <summary>
        /// Returns true if the parameter is empty in spirits, 
        /// even if it contains the separators and white space only
        /// Split on the characters provided.
        /// </summary>
        internal static bool IsParameterEmpty(string parameter, params char[] splitOn)
        {
            if (parameter != null)
            {
                string[] splits = parameter.Split(splitOn, /* omitEmptyEntries */ StringSplitOptions.RemoveEmptyEntries);
                foreach (string s in splits)
                {
                    if (!String.IsNullOrEmpty(s.Trim()))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        /// <summary>
        /// Designed to handle the /link and /embed swithes:
        ///
        ///      /embed[resource]:&lt;filename&gt;[,&lt;name&gt;[,Private]]
        ///      /link[resource]:&lt;filename&gt;[,&lt;name&gt;[,Private]]
        ///
        /// Where the last flag--Private--is either present or not present
        /// depending on whether the ITaskItem has a Private="True" attribue.
        /// </summary>
        /// <param name="switchName"></param>
        /// <param name="parameters"></param>
        /// <param name="metadataNames"></param>
        /// <param name="treatAsFlags"></param>
        internal void AppendSwitchIfNotNull
        (
            string switchName,
            ITaskItem[] parameters,
            string[] metadataNames,
            bool[] treatAsFlags       // May be null. In this case no metadata are treated as flags.
            )
        {
            ErrorUtilities.VerifyThrow
            (
                treatAsFlags == null ||
                (metadataNames != null && metadataNames.Length == treatAsFlags.Length),
                "metadataNames and treatAsFlags should have the same length."
            );

            if (parameters != null)
            {
                foreach (ITaskItem parameter in parameters)
                {
                    AppendSwitchIfNotNull(switchName, parameter.ItemSpec);

                    if (metadataNames != null)
                    {
                        for (int i = 0; i < metadataNames.Length; ++i)
                        {
                            string metadataValue = parameter.GetMetadata(metadataNames[i]);

                            if (!string.IsNullOrEmpty(metadataValue))
                            {
                                // Treat attribute as a boolean flag?
                                if (treatAsFlags == null || !treatAsFlags[i])
                                {
                                    // Not a boolean flag.
                                    CommandLine.Append(',');
                                    AppendTextWithQuoting(metadataValue);
                                }
                                else
                                {
                                    // A boolean flag.
                                    bool flagSet = MetadataConversionUtilities.TryConvertItemMetadataToBool(parameter, metadataNames[i]);
                                    if (flagSet)
                                    {
                                        CommandLine.Append(',');
                                        AppendTextWithQuoting(metadataNames[i]);
                                    }
                                }
                            }
                            else
                            {
                                if (treatAsFlags == null || !treatAsFlags[i])
                                {
                                    // If the caller of this method asked us to add metadata
                                    // A, B, and C, and metadata A doesn't exist on the item,
                                    // then it doesn't make sense to check for B and C.  Because
                                    // since these metadata are just being appended on the
                                    // command-line switch with comma-separation, you can't pass
                                    // in the B metadata unless you've also passed in the A
                                    // metadata.  Otherwise the tool's command-line parser will
                                    // get totally confused.

                                    // This only applies to non-flag attributes.
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Appends a switch if the specified value is <code>true</code>.
        /// </summary>
        internal void AppendSwitchIfTrue
            (
            string switchName,
            bool? value
            )
        {
            if (value.HasValue && value.Value)
            {
                AppendSwitch(switchName);
            }
        }
    }
}
