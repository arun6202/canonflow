
import { newGuid } from "./fable_modules/fable-library-js.5.2.0/Guid.js";
import { add } from "./fable_modules/fable-library-js.5.2.0/Map.js";
import { Auto_generateBoxedEncoderCached_437914C6, Auto_generateBoxedEncoder_437914C6, toString, int64, decimal } from "./fable_modules/Thoth.Json.10.5.1/./Encode.fs.js";
import { Auto_generateBoxedDecoderCached_Z6670B51, int64 as int64_1, decimal as decimal_1 } from "./fable_modules/Thoth.Json.10.5.1/./Decode.fs.js";
import { empty } from "./fable_modules/Thoth.Json.10.5.1/Extra.fs.js";
import { ExtraCoders } from "./fable_modules/Thoth.Json.10.5.1/Types.fs.js";
import { Record, Union } from "./fable_modules/fable-library-js.5.2.0/Types.js";
import { unit_type, class_type, record_type, option_type, union_type, list_type, string_type } from "./fable_modules/fable-library-js.5.2.0/Reflection.js";
import { toArray, truncate, cons, empty as empty_1, append, map as map_1, ofArray, singleton, head, length, isEmpty, choose } from "./fable_modules/fable-library-js.5.2.0/List.js";
import { AnalyticsRequestDto_$reflection, AnalyticsRequestDto, ClientAggregation, ClientPredicate_$reflection, DomainConfig_$reflection, AnalyticsResponseDto_$reflection, OrderLineDocumentDto_$reflection, ClientPredicate } from "../SharedDomain/Dtos.fs.js";
import { format, printf, toText, join, split, substring, concat, isNullOrWhiteSpace } from "./fable_modules/fable-library-js.5.2.0/String.js";
import { FSharpResult$2 } from "./fable_modules/fable-library-js.5.2.0/Result.js";
import { map } from "./fable_modules/fable-library-js.5.2.0/Array.js";
import { Cmd_OfPromise_either, Cmd_none } from "./fable_modules/Fable.Elmish.5.0.2/cmd.fs.js";
import { PromiseBuilder__Delay_62FBFDE1, PromiseBuilder__Run_212F1D4B } from "./fable_modules/Thoth.Fetch.3.0.1/../Fable.Promise.2.0.0/Promise.fs.js";
import { promise } from "./fable_modules/Thoth.Fetch.3.0.1/../Fable.Promise.2.0.0/PromiseImpl.fs.js";
import { FetchError } from "./fable_modules/Thoth.Fetch.3.0.1/Fetch.fs.js";
import { Helper_message, Helper_fetch, Helper_withContentTypeJson, Helper_withProperties } from "./fable_modules/Thoth.Fetch.3.0.1/./Fetch.fs.js";
import { Types_RequestProperties } from "./fable_modules/Fable.Fetch.2.1.0/Fetch.fs.js";
import { keyValueList } from "./fable_modules/fable-library-js.5.2.0/MapUtil.js";
import { unwrap, map as map_2, defaultArg } from "./fable_modules/fable-library-js.5.2.0/Option.js";
import { toString as toString_1 } from "./fable_modules/Thoth.Fetch.3.0.1/../Thoth.Json.10.5.1/Encode.fs.js";
import { fromString } from "./fable_modules/Thoth.Fetch.3.0.1/../Thoth.Json.10.5.1/Decode.fs.js";
import { int64ToString, defaultOf, equals, int32ToString, Exception, uncurry2 } from "./fable_modules/fable-library-js.5.2.0/Util.js";
import * as react from "react";
import { map as map_3, empty as empty_2, singleton as singleton_1, append as append_1, delay, toList } from "./fable_modules/fable-library-js.5.2.0/Seq.js";
import { CSSProp, DOMAttr, HTMLAttr } from "./fable_modules/Fable.React.9.4.0/Fable.React.Props.fs.js";
import { toFloat64 } from "./fable_modules/fable-library-js.5.2.0/BigInt.js";
import { ResponsiveContainer, BarChart, Bar, Legend, Tooltip, YAxis, XAxis, CartesianGrid } from "recharts";
import { ProgramModule_mkProgram, ProgramModule_run } from "./fable_modules/Fable.Elmish.5.0.2/program.fs.js";
import { Program_withReactSynchronous } from "./fable_modules/Fable.Elmish.React.5.6.0/react.fs.js";

export const sharedExtra = (() => {
    let copyOfStruct, copyOfStruct_1;
    const extra_3 = new ExtraCoders((copyOfStruct = newGuid(), copyOfStruct), add("System.Decimal", [decimal, (path) => ((value_1) => decimal_1(path, value_1))], empty.Coders));
    return new ExtraCoders((copyOfStruct_1 = newGuid(), copyOfStruct_1), add("System.Int64", [int64, int64_1], extra_3.Coders));
})();

export class RuleNode extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Condition", "Group"];
    }
}

export function RuleNode_$reflection() {
    return union_type("App.RuleNode", [], RuleNode, () => [[["id", string_type], ["field", string_type], ["op", string_type], ["value", string_type]], [["id", string_type], ["logicalOp", string_type], ["children", list_type(RuleNode_$reflection())]]]);
}

export function compileToAST(node) {
    if (node.tag === 1) {
        const logicalOp = node.fields[1];
        const compiledChildren = choose(compileToAST, node.fields[2]);
        if (isEmpty(compiledChildren)) {
            return undefined;
        }
        else {
            switch (logicalOp) {
                case "And":
                    return new ClientPredicate(5, [compiledChildren]);
                case "Or":
                    return new ClientPredicate(6, [compiledChildren]);
                case "Not":
                    if (length(compiledChildren) > 0) {
                        return new ClientPredicate(7, [head(compiledChildren)]);
                    }
                    else {
                        return undefined;
                    }
                default:
                    return new ClientPredicate(5, [compiledChildren]);
            }
        }
    }
    else {
        const value = node.fields[3];
        const op = node.fields[2];
        const field = node.fields[1];
        if (isNullOrWhiteSpace(value)) {
            return undefined;
        }
        else {
            switch (op) {
                case "Term":
                    return new ClientPredicate(0, [field, value]);
                case "Prefix":
                    return new ClientPredicate(3, [field, value]);
                case "Match":
                    return new ClientPredicate(2, [field, value]);
                default:
                    return new ClientPredicate(0, [field, value]);
            }
        }
    }
}

export class SearchState extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Idle", "Loading", "Success", "Failed"];
    }
}

export function SearchState_$reflection() {
    return union_type("App.SearchState", [], SearchState, () => [[], [], [["Item", list_type(OrderLineDocumentDto_$reflection())]], [["Item", string_type]]]);
}

export class AnalyticsState extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["AIdle", "ALoading", "ASuccess", "AFailed"];
    }
}

export function AnalyticsState_$reflection() {
    return union_type("App.AnalyticsState", [], AnalyticsState, () => [[], [], [["Item", list_type(AnalyticsResponseDto_$reflection())]], [["Item", string_type]]]);
}

export class Tab extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["VisualBuilder", "TextSearch", "Analytics"];
    }
}

export function Tab_$reflection() {
    return union_type("App.Tab", [], Tab, () => [[], [], []]);
}

export class Model extends Record {
    constructor(CurrentTab, RootNode, QueryResult, SearchState, TextQuery, TextParseError, AnalyticsField, AnalyticsAggType, AnalyticsState, DomainConfig) {
        super();
        this.CurrentTab = CurrentTab;
        this.RootNode = RootNode;
        this.QueryResult = QueryResult;
        this.SearchState = SearchState;
        this.TextQuery = TextQuery;
        this.TextParseError = TextParseError;
        this.AnalyticsField = AnalyticsField;
        this.AnalyticsAggType = AnalyticsAggType;
        this.AnalyticsState = AnalyticsState;
        this.DomainConfig = DomainConfig;
    }
}

export function Model_$reflection() {
    return record_type("App.Model", [], Model, () => [["CurrentTab", Tab_$reflection()], ["RootNode", RuleNode_$reflection()], ["QueryResult", string_type], ["SearchState", SearchState_$reflection()], ["TextQuery", string_type], ["TextParseError", option_type(string_type)], ["AnalyticsField", string_type], ["AnalyticsAggType", string_type], ["AnalyticsState", AnalyticsState_$reflection()], ["DomainConfig", option_type(DomainConfig_$reflection())]]);
}

export class Msg extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["SetTab", "AddCondition", "AddGroup", "RemoveNode", "UpdateConditionField", "UpdateConditionOp", "UpdateConditionValue", "UpdateGroupLogicalOp", "RunVisualQuery", "SearchCompleted", "UpdateTextQuery", "RunTextQuery", "UpdateAnalyticsField", "UpdateAnalyticsAggType", "RunAnalytics", "AnalyticsCompleted", "FetchSchema", "SchemaLoaded"];
    }
}

export function Msg_$reflection() {
    return union_type("App.Msg", [], Msg, () => [[["Item", Tab_$reflection()]], [["parentId", string_type]], [["parentId", string_type]], [["id", string_type]], [["id", string_type], ["field", string_type]], [["id", string_type], ["op", string_type]], [["id", string_type], ["value", string_type]], [["id", string_type], ["op", string_type]], [], [["Item", union_type("Microsoft.FSharp.Core.FSharpResult`2", [list_type(OrderLineDocumentDto_$reflection()), class_type("System.Exception")], FSharpResult$2, () => [[["ResultValue", list_type(OrderLineDocumentDto_$reflection())]], [["ErrorValue", class_type("System.Exception")]]])]], [["Item", string_type]], [], [["Item", string_type]], [["Item", string_type]], [], [["Item", union_type("Microsoft.FSharp.Core.FSharpResult`2", [list_type(AnalyticsResponseDto_$reflection()), class_type("System.Exception")], FSharpResult$2, () => [[["ResultValue", list_type(AnalyticsResponseDto_$reflection())]], [["ErrorValue", class_type("System.Exception")]]])]], [], [["Item", union_type("Microsoft.FSharp.Core.FSharpResult`2", [DomainConfig_$reflection(), class_type("System.Exception")], FSharpResult$2, () => [[["ResultValue", DomainConfig_$reflection()]], [["ErrorValue", class_type("System.Exception")]]])]]]);
}

export function newId() {
    let copyOfStruct = newGuid();
    return copyOfStruct;
}

export function createCondition() {
    return new RuleNode(0, [newId(), "Country", "Term", ""]);
}

export function createGroup() {
    return new RuleNode(1, [newId(), "And", singleton(createCondition())]);
}

export function init() {
    return [new Model(new Tab(0, []), new RuleNode(1, [newId(), "And", singleton(createCondition())]), "Click \'Generate JSON Payload\' to compile the tree.", new SearchState(0, []), "Country:USA AND EmployeeLastName:Callahan", undefined, "Country", "Terms", new AnalyticsState(0, []), undefined), singleton((dispatch) => {
        dispatch(new Msg(16, []));
    })];
}

export function parseTextQuery(q) {
    if (isNullOrWhiteSpace(q)) {
        return new FSharpResult$2(1, ["Query cannot be empty"]);
    }
    else {
        const parsedParts = ofArray(map((p) => {
            const p_1 = p.trim();
            const colonIdx = p_1.indexOf(":") | 0;
            if (colonIdx < 0) {
                return new FSharpResult$2(1, [concat("Invalid syntax \'", p_1, "\'. Expected \'field:value\'.")]);
            }
            else {
                const f = substring(p_1, 0, colonIdx).trim();
                if (f === "lineSales") {
                    return new FSharpResult$2(1, ["Range parsing not supported in text parser MVP."]);
                }
                else {
                    return new FSharpResult$2(0, [new ClientPredicate(0, [f, substring(p_1, colonIdx + 1).trim()])]);
                }
            }
        }, split(q, [" AND "], undefined, 1)));
        const errors = choose((_arg) => {
            if (_arg.tag === 1) {
                return _arg.fields[0];
            }
            else {
                return undefined;
            }
        }, parsedParts);
        if (length(errors) > 0) {
            return new FSharpResult$2(1, [join(", ", errors)]);
        }
        else {
            return new FSharpResult$2(0, [new ClientPredicate(5, [choose((_arg_1) => {
                if (_arg_1.tag === 0) {
                    return _arg_1.fields[0];
                }
                else {
                    return undefined;
                }
            }, parsedParts)])]);
        }
    }
}

export function mapTree(id, updater, node) {
    if ((node.tag === 1) ? (node.fields[0] === id) : (node.fields[0] === id)) {
        return updater(node);
    }
    else if (node.tag === 1) {
        return new RuleNode(1, [node.fields[0], node.fields[1], map_1((node_1) => mapTree(id, updater, node_1), node.fields[2])]);
    }
    else {
        return node;
    }
}

export function removeNodeFromTree(id, node) {
    if (node.tag === 1) {
        const gid = node.fields[0];
        if (gid === id) {
            return undefined;
        }
        else {
            return new RuleNode(1, [gid, node.fields[1], choose((node_1) => removeNodeFromTree(id, node_1), node.fields[2])]);
        }
    }
    else if (node.fields[0] === id) {
        return undefined;
    }
    else {
        return node;
    }
}

export function update(msg, model) {
    let matchValue_6;
    switch (msg.tag) {
        case 17:
            if (msg.fields[0].tag === 1) {
                return [new Model(model.CurrentTab, model.RootNode, "Error loading schema: " + msg.fields[0].fields[0].message, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
            }
            else {
                return [new Model(model.CurrentTab, model.RootNode, model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, msg.fields[0].fields[0]), Cmd_none()];
            }
        case 0:
            return [new Model(msg.fields[0], model.RootNode, model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
        case 1:
            return [new Model(model.CurrentTab, mapTree(msg.fields[0], (n) => {
                if (n.tag === 1) {
                    return new RuleNode(1, [n.fields[0], n.fields[1], append(n.fields[2], singleton(createCondition()))]);
                }
                else {
                    return n;
                }
            }, model.RootNode), model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
        case 2:
            return [new Model(model.CurrentTab, mapTree(msg.fields[0], (n_1) => {
                if (n_1.tag === 1) {
                    return new RuleNode(1, [n_1.fields[0], n_1.fields[1], append(n_1.fields[2], singleton(createGroup()))]);
                }
                else {
                    return n_1;
                }
            }, model.RootNode), model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
        case 3: {
            const matchValue_1 = removeNodeFromTree(msg.fields[0], model.RootNode);
            if (matchValue_1 == null) {
                return [new Model(model.CurrentTab, createGroup(), model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
            }
            else {
                return [new Model(model.CurrentTab, matchValue_1, model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
            }
        }
        case 4:
            return [new Model(model.CurrentTab, mapTree(msg.fields[0], (n_2) => {
                if (n_2.tag === 0) {
                    return new RuleNode(0, [n_2.fields[0], msg.fields[1], n_2.fields[2], n_2.fields[3]]);
                }
                else {
                    return n_2;
                }
            }, model.RootNode), model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
        case 5:
            return [new Model(model.CurrentTab, mapTree(msg.fields[0], (n_3) => {
                if (n_3.tag === 0) {
                    return new RuleNode(0, [n_3.fields[0], n_3.fields[1], msg.fields[1], n_3.fields[3]]);
                }
                else {
                    return n_3;
                }
            }, model.RootNode), model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
        case 6:
            return [new Model(model.CurrentTab, mapTree(msg.fields[0], (n_4) => {
                if (n_4.tag === 0) {
                    return new RuleNode(0, [n_4.fields[0], n_4.fields[1], n_4.fields[2], msg.fields[1]]);
                }
                else {
                    return n_4;
                }
            }, model.RootNode), model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
        case 7:
            return [new Model(model.CurrentTab, mapTree(msg.fields[0], (n_5) => {
                if (n_5.tag === 1) {
                    return new RuleNode(1, [n_5.fields[0], msg.fields[1], n_5.fields[2]]);
                }
                else {
                    return n_5;
                }
            }, model.RootNode), model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
        case 8: {
            const matchValue_2 = compileToAST(model.RootNode);
            if (matchValue_2 == null) {
                return [new Model(model.CurrentTab, model.RootNode, "Error: Tree is empty.", new SearchState(0, []), model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
            }
            else {
                const ast = matchValue_2;
                return [new Model(model.CurrentTab, model.RootNode, toString(4, Auto_generateBoxedEncoder_437914C6(ClientPredicate_$reflection(), undefined, undefined, undefined)(ast)), new SearchState(1, []), model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_OfPromise_either(() => PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => {
                    let data_7, caseStrategy_12, extra_12;
                    return ((data_7 = ast, (caseStrategy_12 = undefined, (extra_12 = sharedExtra, (() => {
                        let properties_8;
                        try {
                            const properties_3_1 = Helper_withProperties(undefined, (properties_8 = ofArray([new Types_RequestProperties(0, ["POST"]), new Types_RequestProperties(1, [keyValueList(Helper_withContentTypeJson(data_7, empty_1()), 0)])]), defaultArg(map_2((data_1_2) => cons(new Types_RequestProperties(2, [toString_1(0, Auto_generateBoxedEncoderCached_437914C6(ClientPredicate_$reflection(), unwrap(caseStrategy_12), unwrap(extra_12))(data_1_2))]), properties_8), data_7), properties_8)));
                            const pr_1 = PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (Helper_fetch("http://localhost:5004/api/orders/custom-dsl", properties_3_1).then((_arg_3) => {
                                let response_4, decoder_1_2;
                                return ((response_4 = _arg_3, (decoder_1_2 = defaultArg(undefined, Auto_generateBoxedDecoderCached_Z6670B51(list_type(OrderLineDocumentDto_$reflection()), unwrap(caseStrategy_12), unwrap(extra_12))), PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (((response_4.ok) ? PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (response_4.text().then((_arg_4) => {
                                    let matchValue_3;
                                    return Promise.resolve((matchValue_3 = fromString(uncurry2(decoder_1_2), _arg_4), (matchValue_3.tag === 1) ? (new FSharpResult$2(1, [new FetchError(1, [matchValue_3.fields[0]])])) : (new FSharpResult$2(0, [matchValue_3.fields[0]]))));
                                })))) : (Promise.resolve(new FSharpResult$2(1, [new FetchError(2, [response_4])])))).then((_arg_1_2) => (Promise.resolve(_arg_1_2)))))))));
                            }))));
                            return pr_1.then(void 0, ((arg_3) => (new FSharpResult$2(1, [new FetchError(3, [arg_3])]))));
                        }
                        catch (exn_1) {
                            return PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (Promise.resolve(new FSharpResult$2(1, [new FetchError(0, [exn_1])])))));
                        }
                    })())))).then((_arg_5) => {
                        const result_3 = _arg_5;
                        let response_1_2;
                        if (result_3.tag === 1) {
                            throw new Exception(Helper_message(result_3.fields[0]));
                        }
                        else {
                            response_1_2 = result_3.fields[0];
                        }
                        return Promise.resolve(response_1_2);
                    });
                })), undefined, (arg_4) => (new Msg(9, [new FSharpResult$2(0, [arg_4])])), (arg_5) => (new Msg(9, [new FSharpResult$2(1, [arg_5])])))];
            }
        }
        case 9:
            if (msg.fields[0].tag === 1) {
                return [new Model(model.CurrentTab, model.RootNode, model.QueryResult, new SearchState(3, [msg.fields[0].fields[0].message]), model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
            }
            else {
                return [new Model(model.CurrentTab, model.RootNode, model.QueryResult, new SearchState(2, [msg.fields[0].fields[0]]), model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
            }
        case 10:
            return [new Model(model.CurrentTab, model.RootNode, model.QueryResult, model.SearchState, msg.fields[0], undefined, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
        case 11: {
            const matchValue_4 = parseTextQuery(model.TextQuery);
            if (matchValue_4.tag === 0) {
                const ast_1 = matchValue_4.fields[0];
                return [new Model(model.CurrentTab, model.RootNode, toString(4, Auto_generateBoxedEncoder_437914C6(ClientPredicate_$reflection(), undefined, undefined, undefined)(ast_1)), new SearchState(1, []), model.TextQuery, undefined, model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_OfPromise_either(() => PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => {
                    let data_12, caseStrategy_22, extra_22;
                    return ((data_12 = ast_1, (caseStrategy_22 = undefined, (extra_22 = sharedExtra, (() => {
                        let properties_12;
                        try {
                            const properties_3_2 = Helper_withProperties(undefined, (properties_12 = ofArray([new Types_RequestProperties(0, ["POST"]), new Types_RequestProperties(1, [keyValueList(Helper_withContentTypeJson(data_12, empty_1()), 0)])]), defaultArg(map_2((data_1_3) => cons(new Types_RequestProperties(2, [toString_1(0, Auto_generateBoxedEncoderCached_437914C6(ClientPredicate_$reflection(), unwrap(caseStrategy_22), unwrap(extra_22))(data_1_3))]), properties_12), data_12), properties_12)));
                            const pr_2 = PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (Helper_fetch("http://localhost:5004/api/orders/custom-dsl", properties_3_2).then((_arg_6) => {
                                let response_7, decoder_1_3;
                                return ((response_7 = _arg_6, (decoder_1_3 = defaultArg(undefined, Auto_generateBoxedDecoderCached_Z6670B51(list_type(OrderLineDocumentDto_$reflection()), unwrap(caseStrategy_22), unwrap(extra_22))), PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (((response_7.ok) ? PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (response_7.text().then((_arg_7) => {
                                    let matchValue_5;
                                    return Promise.resolve((matchValue_5 = fromString(uncurry2(decoder_1_3), _arg_7), (matchValue_5.tag === 1) ? (new FSharpResult$2(1, [new FetchError(1, [matchValue_5.fields[0]])])) : (new FSharpResult$2(0, [matchValue_5.fields[0]]))));
                                })))) : (Promise.resolve(new FSharpResult$2(1, [new FetchError(2, [response_7])])))).then((_arg_1_3) => (Promise.resolve(_arg_1_3)))))))));
                            }))));
                            return pr_2.then(void 0, ((arg_6) => (new FSharpResult$2(1, [new FetchError(3, [arg_6])]))));
                        }
                        catch (exn_2) {
                            return PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (Promise.resolve(new FSharpResult$2(1, [new FetchError(0, [exn_2])])))));
                        }
                    })())))).then((_arg_8) => {
                        const result_5 = _arg_8;
                        let response_1_3;
                        if (result_5.tag === 1) {
                            throw new Exception(Helper_message(result_5.fields[0]));
                        }
                        else {
                            response_1_3 = result_5.fields[0];
                        }
                        return Promise.resolve(response_1_3);
                    });
                })), undefined, (arg_7) => (new Msg(9, [new FSharpResult$2(0, [arg_7])])), (arg_8) => (new Msg(9, [new FSharpResult$2(1, [arg_8])])))];
            }
            else {
                return [new Model(model.CurrentTab, model.RootNode, model.QueryResult, model.SearchState, model.TextQuery, matchValue_4.fields[0], model.AnalyticsField, model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
            }
        }
        case 12:
            return [new Model(model.CurrentTab, model.RootNode, model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, msg.fields[0], model.AnalyticsAggType, model.AnalyticsState, model.DomainConfig), Cmd_none()];
        case 13:
            return [new Model(model.CurrentTab, model.RootNode, model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, msg.fields[0], model.AnalyticsState, model.DomainConfig), Cmd_none()];
        case 14: {
            const req = new AnalyticsRequestDto(undefined, singleton((matchValue_6 = model.AnalyticsAggType, (matchValue_6 === "Terms") ? (new ClientAggregation(0, ["myAgg", model.AnalyticsField, 10])) : ((matchValue_6 === "Sum") ? (new ClientAggregation(1, ["myAgg", model.AnalyticsField])) : (new ClientAggregation(0, ["myAgg", model.AnalyticsField, 10]))))));
            return [new Model(model.CurrentTab, model.RootNode, model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, new AnalyticsState(1, []), model.DomainConfig), Cmd_OfPromise_either(() => PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => {
                let data_17, caseStrategy_30, extra_30;
                return ((data_17 = req, (caseStrategy_30 = undefined, (extra_30 = sharedExtra, (() => {
                    let properties_16;
                    try {
                        const properties_3_3 = Helper_withProperties(undefined, (properties_16 = ofArray([new Types_RequestProperties(0, ["POST"]), new Types_RequestProperties(1, [keyValueList(Helper_withContentTypeJson(data_17, empty_1()), 0)])]), defaultArg(map_2((data_1_4) => cons(new Types_RequestProperties(2, [toString_1(0, Auto_generateBoxedEncoderCached_437914C6(AnalyticsRequestDto_$reflection(), unwrap(caseStrategy_30), unwrap(extra_30))(data_1_4))]), properties_16), data_17), properties_16)));
                        const pr_3 = PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (Helper_fetch("http://localhost:5004/api/orders/analytics-dsl", properties_3_3).then((_arg_9) => {
                            let response_10, decoder_1_4;
                            return ((response_10 = _arg_9, (decoder_1_4 = defaultArg(undefined, Auto_generateBoxedDecoderCached_Z6670B51(list_type(AnalyticsResponseDto_$reflection()), unwrap(caseStrategy_30), unwrap(extra_30))), PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (((response_10.ok) ? PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (response_10.text().then((_arg_10) => {
                                let matchValue_7;
                                return Promise.resolve((matchValue_7 = fromString(uncurry2(decoder_1_4), _arg_10), (matchValue_7.tag === 1) ? (new FSharpResult$2(1, [new FetchError(1, [matchValue_7.fields[0]])])) : (new FSharpResult$2(0, [matchValue_7.fields[0]]))));
                            })))) : (Promise.resolve(new FSharpResult$2(1, [new FetchError(2, [response_10])])))).then((_arg_1_4) => (Promise.resolve(_arg_1_4)))))))));
                        }))));
                        return pr_3.then(void 0, ((arg_9) => (new FSharpResult$2(1, [new FetchError(3, [arg_9])]))));
                    }
                    catch (exn_3) {
                        return PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (Promise.resolve(new FSharpResult$2(1, [new FetchError(0, [exn_3])])))));
                    }
                })())))).then((_arg_11) => {
                    const result_7 = _arg_11;
                    let response_1_4;
                    if (result_7.tag === 1) {
                        throw new Exception(Helper_message(result_7.fields[0]));
                    }
                    else {
                        response_1_4 = result_7.fields[0];
                    }
                    return Promise.resolve(response_1_4);
                });
            })), undefined, (arg_10) => (new Msg(15, [new FSharpResult$2(0, [arg_10])])), (arg_11) => (new Msg(15, [new FSharpResult$2(1, [arg_11])])))];
        }
        case 15:
            if (msg.fields[0].tag === 1) {
                return [new Model(model.CurrentTab, model.RootNode, model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, new AnalyticsState(3, [msg.fields[0].fields[0].message]), model.DomainConfig), Cmd_none()];
            }
            else {
                return [new Model(model.CurrentTab, model.RootNode, model.QueryResult, model.SearchState, model.TextQuery, model.TextParseError, model.AnalyticsField, model.AnalyticsAggType, new AnalyticsState(2, [msg.fields[0].fields[0]]), model.DomainConfig), Cmd_none()];
            }
        default:
            return [model, Cmd_OfPromise_either(() => PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => {
                let data_2, caseStrategy_2, extra_2;
                return ((data_2 = undefined, (caseStrategy_2 = undefined, (extra_2 = undefined, (() => {
                    let properties_4;
                    try {
                        const properties_3 = Helper_withProperties(undefined, (properties_4 = ofArray([new Types_RequestProperties(0, ["GET"]), new Types_RequestProperties(1, [keyValueList(Helper_withContentTypeJson(data_2, empty_1()), 0)])]), defaultArg(map_2((data_1_1) => cons(new Types_RequestProperties(2, [toString_1(0, Auto_generateBoxedEncoderCached_437914C6(unit_type, unwrap(caseStrategy_2), unwrap(extra_2))())]), properties_4), data_2), properties_4)));
                        const pr = PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (Helper_fetch("http://localhost:5004/api/schema/Northwind", properties_3).then((_arg) => {
                            let response_1, decoder_1_1;
                            return ((response_1 = _arg, (decoder_1_1 = defaultArg(undefined, Auto_generateBoxedDecoderCached_Z6670B51(DomainConfig_$reflection(), unwrap(caseStrategy_2), unwrap(extra_2))), PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (((response_1.ok) ? PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (response_1.text().then((_arg_1) => {
                                let matchValue;
                                return Promise.resolve((matchValue = fromString(uncurry2(decoder_1_1), _arg_1), (matchValue.tag === 1) ? (new FSharpResult$2(1, [new FetchError(1, [matchValue.fields[0]])])) : (new FSharpResult$2(0, [matchValue.fields[0]]))));
                            })))) : (Promise.resolve(new FSharpResult$2(1, [new FetchError(2, [response_1])])))).then((_arg_1_1) => (Promise.resolve(_arg_1_1)))))))));
                        }))));
                        return pr.then(void 0, ((arg) => (new FSharpResult$2(1, [new FetchError(3, [arg])]))));
                    }
                    catch (exn) {
                        return PromiseBuilder__Run_212F1D4B(promise, PromiseBuilder__Delay_62FBFDE1(promise, () => (Promise.resolve(new FSharpResult$2(1, [new FetchError(0, [exn])])))));
                    }
                })())))).then((_arg_2) => {
                    const result_1 = _arg_2;
                    let response_1_1;
                    if (result_1.tag === 1) {
                        throw new Exception(Helper_message(result_1.fields[0]));
                    }
                    else {
                        response_1_1 = result_1.fields[0];
                    }
                    return Promise.resolve(response_1_1);
                });
            })), undefined, (arg_1) => (new Msg(17, [new FSharpResult$2(0, [arg_1])])), (arg_2) => (new Msg(17, [new FSharpResult$2(1, [arg_2])])))];
    }
}

export function renderDataTable(searchState) {
    switch (searchState.tag) {
        case 1: {
            const props_2 = [["style", {
                color: "#61dafb",
                fontWeight: "bold",
            }]];
            return react.createElement("div", keyValueList(props_2, 1), "Searching Elasticsearch...");
        }
        case 3: {
            const props_4 = [["style", {
                color: "#d40032",
            }]];
            const children_4 = ["Error: " + searchState.fields[0]];
            return react.createElement("div", keyValueList(props_4, 1), ...children_4);
        }
        case 2: {
            const docs = searchState.fields[0];
            const children_38 = toList(delay(() => {
                let props_6, children_6, arg;
                return append_1(singleton_1((props_6 = [["style", {
                    color: "#a6e22e",
                }]], (children_6 = [(arg = (length(docs) | 0), toText(printf("Found %d Hits"))(arg))], react.createElement("h3", keyValueList(props_6, 1), ...children_6)))), delay(() => {
                    let props_36, children_36, children_20, props_18, children_18, props_8, props_10, props_12, props_14, props_16, children_34;
                    return (length(docs) > 0) ? singleton_1((props_36 = [["style", {
                        width: "100%",
                        textAlign: "left",
                        borderCollapse: "collapse",
                        marginTop: "15px",
                    }]], (children_36 = [(children_20 = [(props_18 = [["style", {
                        borderBottom: "1px solid rgba(255,255,255,0.2)",
                    }]], (children_18 = [(props_8 = [["style", {
                        padding: "10px",
                    }]], react.createElement("th", keyValueList(props_8, 1), "Order ID")), (props_10 = [["style", {
                        padding: "10px",
                    }]], react.createElement("th", keyValueList(props_10, 1), "Customer")), (props_12 = [["style", {
                        padding: "10px",
                    }]], react.createElement("th", keyValueList(props_12, 1), "Country")), (props_14 = [["style", {
                        padding: "10px",
                    }]], react.createElement("th", keyValueList(props_14, 1), "Product")), (props_16 = [["style", {
                        padding: "10px",
                    }]], react.createElement("th", keyValueList(props_16, 1), "Sales ($)"))], react.createElement("tr", keyValueList(props_18, 1), ...children_18)))], react.createElement("thead", {}, ...children_20)), (children_34 = map_1((doc) => {
                        let props_22, children_22, props_24, props_26, props_28, props_30, children_30;
                        const props_32 = [["style", {
                            borderBottom: "1px solid rgba(255,255,255,0.05)",
                        }]];
                        const children_32 = [(props_22 = [["style", {
                            padding: "10px",
                        }]], (children_22 = [int32ToString(doc.OrderId)], react.createElement("td", keyValueList(props_22, 1), ...children_22))), (props_24 = [["style", {
                            padding: "10px",
                        }]], react.createElement("td", keyValueList(props_24, 1), doc.Customer.CompanyName)), (props_26 = [["style", {
                            padding: "10px",
                        }]], react.createElement("td", keyValueList(props_26, 1), doc.Customer.Country)), (props_28 = [["style", {
                            padding: "10px",
                        }]], react.createElement("td", keyValueList(props_28, 1), doc.Product.ProductName)), (props_30 = [["style", {
                            padding: "10px",
                            color: "#61dafb",
                            fontWeight: "bold",
                        }]], (children_30 = [format('{0:' + "F2" + '}', doc.LineSales)], react.createElement("td", keyValueList(props_30, 1), ...children_30)))];
                        return react.createElement("tr", keyValueList(props_32, 1), ...children_32);
                    }, truncate(50, docs)), react.createElement("tbody", {}, ...children_34))], react.createElement("table", keyValueList(props_36, 1), ...children_36)))) : empty_2();
                }));
            }));
            return react.createElement("div", {}, ...children_38);
        }
        default:
            return react.createElement("p", {}, "Ready to search.");
    }
}

export function viewNode(model, node, dispatch) {
    let props_34, children_33, props_26, children_25, props_28, props_30, children_35, children_4, children_12;
    if (node.tag === 1) {
        const id_1 = node.fields[0];
        const props_38 = [new HTMLAttr(64, ["glass-card group-container"]), ["style", {
            marginBottom: "15px",
        }]];
        const children_37 = [(props_34 = [["style", {
            display: "flex",
            alignItems: "center",
            marginBottom: "15px",
        }]], (children_33 = [(props_26 = [new DOMAttr(9, [(e_3) => {
            dispatch(new Msg(7, [id_1, e_3.target.value]));
        }]), new HTMLAttr(161, [node.fields[1]]), ["style", {
            fontWeight: "800",
            color: "#61dafb",
            marginRight: "15px",
            background: "rgba(97, 218, 251, 0.1)",
            borderColor: "rgba(97, 218, 251, 0.3)",
        }]], (children_25 = [react.createElement("option", {
            value: "And",
        }, "AND"), react.createElement("option", {
            value: "Or",
        }, "OR"), react.createElement("option", {
            value: "Not",
        }, "NOT")], react.createElement("select", keyValueList(props_26, 1), ...children_25))), (props_28 = [new HTMLAttr(64, ["btn-secondary"]), new DOMAttr(40, [(_arg_1) => {
            dispatch(new Msg(1, [id_1]));
        }]), ["style", {
            marginRight: "10px",
        }]], react.createElement("button", keyValueList(props_28, 1), "+ Rule")), (props_30 = [new HTMLAttr(64, ["btn-secondary"]), new DOMAttr(40, [(_arg_2) => {
            dispatch(new Msg(2, [id_1]));
        }]), ["style", {
            marginRight: "auto",
        }]], react.createElement("button", keyValueList(props_30, 1), "+ Group")), react.createElement("button", {
            className: "btn-danger",
            onClick: (_arg_3) => {
                dispatch(new Msg(3, [id_1]));
            },
        }, "✕")], react.createElement("div", keyValueList(props_34, 1), ...children_33))), (children_35 = map_1((c) => viewNode(model, c, dispatch), node.fields[2]), react.createElement("div", {
            className: "rule-children",
        }, ...children_35))];
        return react.createElement("div", keyValueList(props_38, 1), ...children_37);
    }
    else {
        const id = node.fields[0];
        const children_16 = [(children_4 = toList(delay(() => {
            const matchValue = model.DomainConfig;
            return (matchValue == null) ? singleton_1(react.createElement("option", {
                value: "Loading...",
            }, "Loading Schema...")) : map_3((f) => react.createElement("option", {
                value: f.Name,
            }, f.DisplayName), matchValue.Fields);
        })), react.createElement("select", {
            onChange: (e) => {
                dispatch(new Msg(4, [id, e.target.value]));
            },
            value: node.fields[1],
        }, ...children_4)), (children_12 = [react.createElement("option", {
            value: "Term",
        }, "Equals (Term)"), react.createElement("option", {
            value: "Prefix",
        }, "Starts With (Prefix)"), react.createElement("option", {
            value: "Match",
        }, "Full Text (Match)")], react.createElement("select", {
            onChange: (e_1) => {
                dispatch(new Msg(5, [id, e_1.target.value]));
            },
            value: node.fields[2],
        }, ...children_12)), react.createElement("input", {
            type: "text",
            placeholder: "Enter value...",
            value: node.fields[3],
            onChange: (e_2) => {
                dispatch(new Msg(6, [id, e_2.target.value]));
            },
        }), react.createElement("button", {
            className: "btn-danger",
            onClick: (_arg) => {
                dispatch(new Msg(3, [id]));
            },
            title: "Remove Rule",
        }, "✕")];
        return react.createElement("div", {
            className: "rule-row",
        }, ...children_16);
    }
}

export function view(model, dispatch) {
    const props_114 = [["style", {
        maxWidth: "900px",
        margin: "0 auto",
    }]];
    const children_106 = toList(delay(() => {
        let props;
        return append_1(singleton_1((props = [["style", {
            margin: "0 0 20px 0",
            color: "#fff",
            textShadow: "0 0 20px rgba(97, 218, 251, 0.5)",
        }]], react.createElement("h1", keyValueList(props, 1), "SOTA Search Platform"))), delay(() => {
            let props_8, children_8;
            return append_1(singleton_1((props_8 = [["style", {
                display: "flex",
                marginBottom: "20px",
                borderBottom: "2px solid rgba(255,255,255,0.1)",
            }]], (children_8 = toList(delay(() => {
                let props_2;
                const tabStyle = (isActive) => ofArray([new CSSProp(273, ["10px 20px"]), new CSSProp(123, ["pointer"]), new CSSProp(165, ["bold"]), new CSSProp(103, [isActive ? "#61dafb" : "#aaa"]), new CSSProp(42, [isActive ? "3px solid #61dafb" : "3px solid transparent"])]);
                return append_1(singleton_1((props_2 = [new DOMAttr(40, [(_arg) => {
                    dispatch(new Msg(0, [new Tab(0, [])]));
                }]), ["style", keyValueList(tabStyle(equals(model.CurrentTab, new Tab(0, []))), 1)]], react.createElement("div", keyValueList(props_2, 1), "Visual Builder"))), delay(() => {
                    let props_4;
                    return append_1(singleton_1((props_4 = [new DOMAttr(40, [(_arg_1) => {
                        dispatch(new Msg(0, [new Tab(1, [])]));
                    }]), ["style", keyValueList(tabStyle(equals(model.CurrentTab, new Tab(1, []))), 1)]], react.createElement("div", keyValueList(props_4, 1), "Text Search"))), delay(() => {
                        let props_6;
                        return singleton_1((props_6 = [new DOMAttr(40, [(_arg_2) => {
                            dispatch(new Msg(0, [new Tab(2, [])]));
                        }]), ["style", keyValueList(tabStyle(equals(model.CurrentTab, new Tab(2, []))), 1)]], react.createElement("div", keyValueList(props_6, 1), "Analytics")));
                    }));
                }));
            })), react.createElement("div", keyValueList(props_8, 1), ...children_8)))), delay(() => {
                let children_36, children_32, props_36, children_34, children_104, children_56, props_40, children_52, children_44, children_50, props_56, props_110, children_102, children_22, props_12, children_12, props_10, props_14, children_14, props_20, children_20, props_16, props_18;
                const matchValue = model.CurrentTab;
                return (matchValue.tag === 1) ? singleton_1((children_36 = [(children_32 = toList(delay(() => {
                    let props_24;
                    return append_1(singleton_1((props_24 = [["style", {
                        marginTop: "0",
                    }]], react.createElement("h3", keyValueList(props_24, 1), "Google-Style Text Parser"))), delay(() => {
                        let props_26;
                        return append_1(singleton_1((props_26 = [["style", {
                            color: "#aaa",
                        }]], react.createElement("p", keyValueList(props_26, 1), "Try typing: country:USA AND lastName:Callahan"))), delay(() => {
                            let props_28;
                            return append_1(singleton_1((props_28 = [new HTMLAttr(159, ["text"]), new HTMLAttr(161, [model.TextQuery]), new DOMAttr(9, [(e) => {
                                dispatch(new Msg(10, [e.target.value]));
                            }]), ["style", {
                                width: "100%",
                                padding: "15px",
                                fontSize: "18px",
                                background: "rgba(255,255,255,0.1)",
                                color: "#fff",
                                border: "1px solid rgba(255,255,255,0.2)",
                                borderRadius: "8px",
                            }]], react.createElement("input", keyValueList(props_28, 1)))), delay(() => {
                                let matchValue_1, err, props_30;
                                return append_1((matchValue_1 = model.TextParseError, (matchValue_1 == null) ? singleton_1(defaultOf()) : ((err = matchValue_1, singleton_1((props_30 = [["style", {
                                    color: "#d40032",
                                    fontWeight: "bold",
                                }]], react.createElement("p", keyValueList(props_30, 1), err)))))), delay(() => {
                                    let props_32;
                                    return singleton_1((props_32 = [new DOMAttr(40, [(_arg_4) => {
                                        dispatch(new Msg(11, []));
                                    }]), ["style", {
                                        marginTop: "20px",
                                        padding: "12px 24px",
                                        fontSize: "16px",
                                        boxShadow: "0 4px 15px rgba(0, 120, 212, 0.4)",
                                    }]], react.createElement("button", keyValueList(props_32, 1), "Parse & Execute")));
                                }));
                            }));
                        }));
                    }));
                })), react.createElement("div", {
                    className: "glass-card",
                }, ...children_32)), (props_36 = [new HTMLAttr(64, ["glass-card"]), ["style", {
                    marginTop: "30px",
                }]], (children_34 = [renderDataTable(model.SearchState)], react.createElement("div", keyValueList(props_36, 1), ...children_34)))], react.createElement("div", {}, ...children_36))) : ((matchValue.tag === 2) ? singleton_1((children_104 = [(children_56 = [(props_40 = [["style", {
                    marginTop: "0",
                }]], react.createElement("h3", keyValueList(props_40, 1), "Dynamic Analytics Builder")), (children_52 = [(children_44 = [react.createElement("option", {
                    value: "Terms",
                }, "Group By (Terms)"), react.createElement("option", {
                    value: "Sum",
                }, "Total Metric (Sum)")], react.createElement("select", {
                    onChange: (e_1) => {
                        dispatch(new Msg(13, [e_1.target.value]));
                    },
                    value: model.AnalyticsAggType,
                }, ...children_44)), (children_50 = toList(delay(() => {
                    const matchValue_2 = model.DomainConfig;
                    return (matchValue_2 == null) ? singleton_1(react.createElement("option", {
                        value: "Loading...",
                    }, "Loading Schema...")) : map_3((f) => react.createElement("option", {
                        value: f.Name,
                    }, f.DisplayName), matchValue_2.Fields);
                })), react.createElement("select", {
                    onChange: (e_2) => {
                        dispatch(new Msg(12, [e_2.target.value]));
                    },
                    value: model.AnalyticsField,
                }, ...children_50))], react.createElement("div", {
                    className: "rule-row",
                }, ...children_52)), (props_56 = [new DOMAttr(40, [(_arg_5) => {
                    dispatch(new Msg(14, []));
                }]), ["style", {
                    marginTop: "20px",
                    padding: "12px 24px",
                    fontSize: "16px",
                    boxShadow: "0 4px 15px rgba(0, 120, 212, 0.4)",
                }]], react.createElement("button", keyValueList(props_56, 1), "Run Analytics"))], react.createElement("div", {
                    className: "glass-card",
                }, ...children_56)), (props_110 = [new HTMLAttr(64, ["glass-card"]), ["style", {
                    marginTop: "30px",
                }]], (children_102 = toList(delay(() => {
                    let props_62, props_64, children_62, children_100, props_66;
                    const matchValue_3 = model.AnalyticsState;
                    return (matchValue_3.tag === 1) ? singleton_1((props_62 = [["style", {
                        color: "#61dafb",
                        fontWeight: "bold",
                    }]], react.createElement("div", keyValueList(props_62, 1), "Crunching data..."))) : ((matchValue_3.tag === 3) ? singleton_1((props_64 = [["style", {
                        color: "#d40032",
                    }]], (children_62 = ["Error: " + matchValue_3.fields[0]], react.createElement("div", keyValueList(props_64, 1), ...children_62)))) : ((matchValue_3.tag === 2) ? singleton_1((children_100 = cons((props_66 = [["style", {
                        color: "#a6e22e",
                    }]], react.createElement("h3", keyValueList(props_66, 1), "Business Intelligence Results")), map_1((aggRes) => {
                        let props_68, props_86, children_78, props_84, children_76, props_82, children_74, props_70, props_72, props_74, props_76, props_78, props_80, props_104, children_96, children_86, props_92, children_84, props_88, props_90, children_94;
                        const chartData = toArray(map_1((b) => {
                            let matchValue_4;
                            return {
                                name: b.Key,
                                value: (matchValue_4 = b.SubValue, (matchValue_4 == null) ? toFloat64(b.DocCount) : matchValue_4),
                            };
                        }, aggRes.Buckets));
                        const props_106 = [["style", {
                            marginBottom: "20px",
                        }]];
                        const children_98 = [(props_68 = [["style", {
                            color: "#61dafb",
                            borderBottom: "1px solid rgba(255,255,255,0.2)",
                            paddingBottom: "10px",
                        }]], react.createElement("h4", keyValueList(props_68, 1), aggRes.AggName)), (props_86 = [new HTMLAttr(64, ["recharts-wrapper"]), ["style", {
                            marginTop: "20px",
                            marginBottom: "20px",
                            height: "300px",
                        }]], (children_78 = [(props_84 = {
                            height: "100%",
                            width: "100%",
                        }, (children_76 = singleton((props_82 = {
                            data: chartData,
                            margin: {
                                bottom: 5,
                                left: 20,
                                right: 30,
                                top: 20,
                            },
                        }, (children_74 = ofArray([(props_70 = {
                            stroke: "rgba(255,255,255,0.1)",
                            strokeDasharray: "3 3",
                        }, react.createElement(CartesianGrid, props_70)), (props_72 = {
                            dataKey: "name",
                            stroke: "#a0a0a0",
                        }, react.createElement(XAxis, props_72)), (props_74 = {
                            stroke: "#a0a0a0",
                        }, react.createElement(YAxis, props_74)), (props_76 = {
                            contentStyle: {
                                backgroundColor: "rgba(0,0,0,0.8)",
                                border: "1px solid #333",
                                borderRadius: "4px",
                                color: "#fff",
                            },
                        }, react.createElement(Tooltip, props_76)), (props_78 = {}, react.createElement(Legend, props_78)), (props_80 = {
                            dataKey: "value",
                            fill: "#61dafb",
                            radius: new Int32Array([4, 4, 0, 0]),
                        }, react.createElement(Bar, props_80))]), react.createElement(BarChart, props_82, ...children_74)))), react.createElement(ResponsiveContainer, props_84, ...children_76)))], react.createElement("div", keyValueList(props_86, 1), ...children_78))), (props_104 = [["style", {
                            width: "100%",
                            textAlign: "left",
                            borderCollapse: "collapse",
                        }]], (children_96 = [(children_86 = [(props_92 = [["style", {
                            borderBottom: "1px solid rgba(255,255,255,0.1)",
                        }]], (children_84 = [(props_88 = [["style", {
                            padding: "10px",
                        }]], react.createElement("th", keyValueList(props_88, 1), "Bucket Key")), (props_90 = [["style", {
                            padding: "10px",
                        }]], react.createElement("th", keyValueList(props_90, 1), "Value"))], react.createElement("tr", keyValueList(props_92, 1), ...children_84)))], react.createElement("thead", {}, ...children_86)), (children_94 = map_1((b_1) => {
                            let props_96, props_98, children_90;
                            const props_100 = [["style", {
                                borderBottom: "1px solid rgba(255,255,255,0.05)",
                            }]];
                            const children_92 = [(props_96 = [["style", {
                                padding: "10px",
                            }]], react.createElement("td", keyValueList(props_96, 1), b_1.Key)), (props_98 = [["style", {
                                padding: "10px",
                                color: "#61dafb",
                                fontWeight: "bold",
                            }]], (children_90 = toList(delay(() => {
                                const matchValue_5 = b_1.SubValue;
                                if (matchValue_5 == null) {
                                    return singleton_1(int64ToString(b_1.DocCount) + " docs");
                                }
                                else {
                                    const v_1 = matchValue_5;
                                    return singleton_1(toText(printf("%.2f"))(v_1));
                                }
                            })), react.createElement("td", keyValueList(props_98, 1), ...children_90)))];
                            return react.createElement("tr", keyValueList(props_100, 1), ...children_92);
                        }, aggRes.Buckets), react.createElement("tbody", {}, ...children_94))], react.createElement("table", keyValueList(props_104, 1), ...children_96)))];
                        return react.createElement("div", keyValueList(props_106, 1), ...children_98);
                    }, matchValue_3.fields[0])), react.createElement("div", {}, ...children_100))) : singleton_1(react.createElement("p", {}, "Ready for BI query."))));
                })), react.createElement("div", keyValueList(props_110, 1), ...children_102)))], react.createElement("div", {}, ...children_104))) : singleton_1((children_22 = [(props_12 = [["style", {
                    display: "flex",
                    justifyContent: "flex-end",
                    marginBottom: "20px",
                }]], (children_12 = [(props_10 = [new DOMAttr(40, [(_arg_3) => {
                    dispatch(new Msg(8, []));
                }]), ["style", {
                    padding: "12px 24px",
                    fontSize: "16px",
                    boxShadow: "0 4px 15px rgba(0, 120, 212, 0.4)",
                }]], react.createElement("button", keyValueList(props_10, 1), "Execute Visual Query"))], react.createElement("div", keyValueList(props_12, 1), ...children_12))), viewNode(model, model.RootNode, dispatch), (props_14 = [new HTMLAttr(64, ["glass-card"]), ["style", {
                    marginTop: "30px",
                }]], (children_14 = [renderDataTable(model.SearchState)], react.createElement("div", keyValueList(props_14, 1), ...children_14))), (props_20 = [new HTMLAttr(64, ["glass-card"]), ["style", {
                    marginTop: "30px",
                }]], (children_20 = [(props_16 = [["style", {
                    marginTop: "0",
                    color: "#61dafb",
                }]], react.createElement("h3", keyValueList(props_16, 1), "Compiled AST Payload:")), (props_18 = [["style", {
                    margin: "0",
                    maxHeight: "200px",
                    color: "#a6e22e",
                    fontSize: "14px",
                    fontFamily: "Consolas, monospace",
                    overflowY: "auto",
                }]], react.createElement("pre", keyValueList(props_18, 1), model.QueryResult))], react.createElement("div", keyValueList(props_20, 1), ...children_20)))], react.createElement("div", {}, ...children_22))));
            }));
        }));
    }));
    return react.createElement("div", keyValueList(props_114, 1), ...children_106);
}

ProgramModule_run(Program_withReactSynchronous("elmish-app", ProgramModule_mkProgram(init, update, view)));

