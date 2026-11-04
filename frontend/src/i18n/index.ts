import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import LanguageDetector from "i18next-browser-languagedetector";

import commonZh from "./locales/zh-CN/common.json";
import layoutZh from "./locales/zh-CN/layout.json";
import projectsZh from "./locales/zh-CN/projects.json";
import workitemsZh from "./locales/zh-CN/workitems.json";
import prototypeZh from "./locales/zh-CN/prototype.json";
import kanbanZh from "./locales/zh-CN/kanban.json";
import iterationsZh from "./locales/zh-CN/iterations.json";
import ganttZh from "./locales/zh-CN/gantt.json";
import reportZh from "./locales/zh-CN/report.json";
import providersZh from "./locales/zh-CN/providers.json";
import agentsZh from "./locales/zh-CN/agents.json";
import agentrunsZh from "./locales/zh-CN/agentruns.json";
import commonEn from "./locales/en/common.json";
import layoutEn from "./locales/en/layout.json";
import projectsEn from "./locales/en/projects.json";
import workitemsEn from "./locales/en/workitems.json";
import prototypeEn from "./locales/en/prototype.json";
import kanbanEn from "./locales/en/kanban.json";
import iterationsEn from "./locales/en/iterations.json";
import ganttEn from "./locales/en/gantt.json";
import reportEn from "./locales/en/report.json";
import providersEn from "./locales/en/providers.json";
import agentsEn from "./locales/en/agents.json";
import agentrunsEn from "./locales/en/agentruns.json";

/** 默认语言：简体中文 */
export const defaultLanguage = "zh-CN";

export const supportedLanguages = [
  { value: "zh-CN", label: "简体中文" },
  { value: "en", label: "English" },
];

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      "zh-CN": {
        common: commonZh,
        layout: layoutZh,
        projects: projectsZh,
        workitems: workitemsZh,
        prototype: prototypeZh,
        kanban: kanbanZh,
        iterations: iterationsZh,
        gantt: ganttZh,
        report: reportZh,
        providers: providersZh,
        agents: agentsZh,
        agentruns: agentrunsZh,
      },
      en: {
        common: commonEn,
        layout: layoutEn,
        projects: projectsEn,
        workitems: workitemsEn,
        prototype: prototypeEn,
        kanban: kanbanEn,
        iterations: iterationsEn,
        gantt: ganttEn,
        report: reportEn,
        providers: providersEn,
        agents: agentsEn,
        agentruns: agentrunsEn,
      },
    },
    fallbackLng: defaultLanguage,
    defaultNS: "common",
    interpolation: {
      escapeValue: false,
    },
    detection: {
      order: ["localStorage", "navigator"],
      caches: ["localStorage"],
    },
  });

export default i18n;
